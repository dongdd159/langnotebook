using System.Text.Json;
using LangNotebook.Data;
using LangNotebook.Helper;
using Microsoft.JSInterop;

namespace LangNotebook.Service;

/// <summary>
/// Backs the app's data up to the user's own Dropbox or Google Drive and restores it. The sign-in and the provider
/// calls live in wwwroot/js/cloud.js; this class decides when to back up, keeps the status the UI shows and turns
/// results into messages.
/// </summary>
public sealed class CloudBackupService : IDisposable
{
    // Backups are sent once the data has been quiet for a while, and never more often than the minimum interval.
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

    private readonly IJSRuntime _js;
    private readonly BackupService _backup;
    private readonly BrowserDbStorage _storage;
    private readonly AppState _state;
    private CancellationTokenSource? _timer;
    private bool _initialized;
    private bool _dirty;
    private bool _busy;
    private string _lastErrorCode = string.Empty;

    public CloudBackupService(IJSRuntime js, BackupService backup, BrowserDbStorage storage, AppState state)
    {
        _js = js;
        _backup = backup;
        _storage = storage;
        _state = state;
    }

    public event Action? Changed;

    public CloudStatus Status { get; private set; } = new();
    public IReadOnlyList<CloudProviderInfo> Providers { get; private set; } = [];
    public IReadOnlyList<CloudFile> Files { get; private set; } = [];
    public bool IsBusy => _busy;
    public string LastErrorMessage { get; private set; } = string.Empty;

    public CloudSyncState State
    {
        get
        {
            if (!Status.Connected) return CloudSyncState.Disconnected;
            if (_busy) return CloudSyncState.Working;
            if (Status.NeedsReconnect) return CloudSyncState.NeedsReconnect;
            if (!Status.Armed) return CloudSyncState.DecisionNeeded;
            if (_lastErrorCode is "offline" or "network") return CloudSyncState.Offline;
            if (_lastErrorCode.Length > 0) return CloudSyncState.Error;
            return _dirty ? CloudSyncState.Pending : CloudSyncState.Synced;
        }
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;
        _initialized = true;
        var result = await CallAsync("providers");
        Providers = result.Providers ?? [];
        ApplyStatus(result.Status);
        _storage.Persisted += OnPersisted;
        // Changes made since the last session may not be backed up yet. The upload is skipped if nothing differs.
        if (Status.Connected)
        {
            _dirty = true;
            ScheduleAutoBackup();
        }
        Notify();
    }

    // ------------------------------------------------------------------ connect and disconnect

    public async Task<CloudResult> ConnectAsync(string providerId)
    {
        var result = await CallAsync("connect", providerId);
        ApplyStatus(result.Status);
        if (!result.Ok)
        {
            Notify();
            return result;
        }
        _lastErrorCode = string.Empty;

        // A device whose own earlier backups are there, or a cloud with no backups at all, can start backing up at
        // once. Otherwise the user decides first, so an empty device can't push out the backups that are there.
        var list = await RefreshFilesAsync();
        if (list.Ok && (Files.Count == 0 || Files.Any(x => x.ThisDevice)))
        {
            await SetArmedAsync(true);
            if (Files.Count == 0)
                await BackupNowAsync(true);
        }
        Notify();
        return result;
    }

    public Task CancelConnectAsync() => CallAsync("cancelConnect");

    public async Task<CloudResult> DisconnectAsync()
    {
        _timer?.Cancel();
        var result = await CallAsync("disconnect");
        ApplyStatus(result.Status);
        _dirty = false;
        _lastErrorCode = string.Empty;
        Files = [];
        Notify();
        return result;
    }

    public async Task SetAutoBackupAsync(bool enabled)
    {
        ApplyStatus((await CallAsync("setAutoBackup", enabled)).Status);
        if (enabled)
            ScheduleAutoBackup();
        else
            _timer?.Cancel();
        Notify();
    }

    public async Task SetArmedAsync(bool armed)
    {
        ApplyStatus((await CallAsync("setArmed", armed)).Status);
        if (armed)
            ScheduleAutoBackup();
        Notify();
    }

    // ------------------------------------------------------------------ backup

    public async Task<CloudResult> BackupNowAsync(bool force = false)
    {
        if (_busy)
            return Fail("busy", "Another backup is already running.");
        _busy = true;
        _timer?.Cancel();
        Notify();
        try
        {
            var json = await _backup.ExportJsonAsync();
            var result = await CallAsync("backup", json, force);
            ApplyStatus(result.Status);
            if (result.Ok)
            {
                _dirty = false;
                _lastErrorCode = string.Empty;
                LastErrorMessage = string.Empty;
                if (result.Uploaded)
                    await RefreshFilesAsync();
            }
            else
            {
                _lastErrorCode = result.Code;
                LastErrorMessage = result.Message;
            }
            return result;
        }
        finally
        {
            _busy = false;
            Notify();
        }
    }

    private void OnPersisted()
    {
        if (!Status.Connected)
            return;
        _dirty = true;
        ScheduleAutoBackup();
        Notify();
    }

    private void ScheduleAutoBackup(TimeSpan? delay = null)
    {
        if (!Status.Connected || !Status.AutoBackup || !Status.Armed || Status.NeedsReconnect)
            return;
        _timer?.Cancel();
        var source = _timer = new CancellationTokenSource();
        _ = RunAutoBackupAsync(delay ?? NextDelay(), source.Token);
    }

    private TimeSpan NextDelay()
    {
        if (Status.LastBackup is not { } last)
            return QuietPeriod;
        var untilAllowed = MinInterval - (DateTime.UtcNow - last);
        return untilAllowed > QuietPeriod ? untilAllowed : QuietPeriod;
    }

    private async Task RunAutoBackupAsync(TimeSpan wait, CancellationToken token)
    {
        try
        {
            await Task.Delay(wait, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }
        if (!_dirty || _busy)
            return;
        var result = await BackupNowAsync();
        // Needs the user (sign in again, storage full): retrying on a timer would only repeat the failure.
        if (!result.Ok && result.Code is "offline" or "network" or "provider")
            ScheduleAutoBackup(RetryDelay);
    }

    // ------------------------------------------------------------------ restore

    public async Task<CloudResult> RefreshFilesAsync()
    {
        var result = await CallAsync("list");
        ApplyStatus(result.Status);
        if (result.Ok)
            Files = (result.Files ?? []).OrderByDescending(x => x.Created).ToList();
        Notify();
        return result;
    }

    /// <summary>
    /// Replaces the data on this device with a backup. The backup is read and checked, and a copy of the current data
    /// is kept for "Undo", before anything is changed.
    /// </summary>
    public async Task<CloudResult> RestoreAsync(string fileId)
    {
        if (_busy)
            return Fail("busy", "Another backup is already running.");
        _busy = true;
        _timer?.Cancel();
        Notify();
        try
        {
            var download = await CallAsync("download", fileId);
            ApplyStatus(download.Status);
            if (!download.Ok || string.IsNullOrEmpty(download.Json))
                return download.Ok ? Fail("invalid", "The backup file is empty.") : download;

            try
            {
                BackupService.ParseBackup(download.Json);
            }
            catch (InvalidOperationException ex)
            {
                return Fail("invalid", ex.Message);
            }

            var snapshot = await CallAsync("saveSnapshot", await _backup.ExportJsonAsync());
            if (!snapshot.Ok)
                return Fail("snapshot", snapshot.Message);

            try
            {
                await _backup.ImportJsonAsync(download.Json, true);
            }
            catch (Exception ex)
            {
                await CallAsync("clearSnapshot");
                return Fail("invalid", ex.Message);
            }
            _state.ActiveNotebookId = null;
            _state.NotifyChanged();
            await SetArmedAsync(true);
            _lastErrorCode = string.Empty;
            ApplyStatus((await CallAsync("status")).Status);
            return new CloudResult { Ok = true, Status = Status };
        }
        finally
        {
            _busy = false;
            Notify();
        }
    }

    public async Task<CloudResult> UndoRestoreAsync()
    {
        if (_busy)
            return Fail("busy", "Another backup is already running.");
        _busy = true;
        Notify();
        try
        {
            var snapshot = await CallAsync("loadSnapshot");
            if (!snapshot.Ok || string.IsNullOrEmpty(snapshot.Json))
                return snapshot.Ok ? Fail("invalid", "There is nothing to undo.") : snapshot;
            try
            {
                await _backup.ImportJsonAsync(snapshot.Json, true);
            }
            catch (Exception ex)
            {
                return Fail("invalid", ex.Message);
            }
            await CallAsync("clearSnapshot");
            _state.ActiveNotebookId = null;
            _state.NotifyChanged();
            ApplyStatus((await CallAsync("status")).Status);
            return new CloudResult { Ok = true, Status = Status };
        }
        finally
        {
            _busy = false;
            Notify();
        }
    }

    // ------------------------------------------------------------------ messages

    /// <summary>Shows the outcome of an action as a toast, in plain words: what happened and what to do next.</summary>
    public void Report(CloudResult result, CloudAction action)
    {
        var provider = Status.ProviderName.Length > 0 ? Status.ProviderName : "the cloud";
        if (result.Ok)
        {
            switch (action)
            {
                case CloudAction.Connect:
                    _state.ShowSuccess(Status.Account.Length > 0
                        ? $"Connected to {provider} as {Status.Account}."
                        : $"Connected to {provider}.");
                    break;
                case CloudAction.Disconnect:
                    _state.ShowSuccess(
                        "Disconnected. Your data on this device and your backups in the cloud are unchanged.");
                    break;
                case CloudAction.Restore:
                    _state.ShowSuccess("Backup restored. Your previous data was saved, so you can undo this in Settings.");
                    break;
                case CloudAction.Undo:
                    _state.ShowSuccess("Restore undone. Your previous data is back.");
                    break;
                default:
                    _state.ShowSuccess(result.Skipped == "unchanged"
                        ? "Already backed up. Nothing has changed since the last backup."
                        : $"Backed up to {provider}.");
                    break;
            }
            return;
        }

        _state.ShowError(result.Code switch
        {
            "offline" or "network" => action == CloudAction.Backup
                ? "Couldn't back up. You're offline. We'll try again when you're back online."
                : "Couldn't reach the cloud. Check your connection and try again.",
            "auth-expired" => $"Couldn't back up. Your {provider} sign-in has expired. Reconnect to continue.",
            "popup-blocked" => "Couldn't open the sign-in window. Allow pop-ups for this site, then try again.",
            "cancelled" => "Sign-in cancelled. Nothing was changed.",
            "quota" => $"Couldn't back up. Your {provider} storage is full. Free up space and try again.",
            "not-configured" => $"{provider} isn't set up in this version of the app.",
            "busy" => "A backup is already running. Try again in a moment.",
            "snapshot" =>
                "Nothing was changed: there's no room to keep a copy of your current data first. Download a JSON backup in Backup & data, then try again.",
            "invalid" => $"Couldn't use that backup. {result.Message}",
            _ => $"Couldn't finish. {result.Message} Try again in a moment."
        });
    }

    // ------------------------------------------------------------------ plumbing

    private async Task<CloudResult> CallAsync(string function, params object?[] args)
    {
        try
        {
            var json = await _js.InvokeAsync<string>($"langCloud.{function}", args);
            return JsonSerializer.Deserialize(json, CloudJsonContext.Default.CloudResult)
                   ?? Fail("provider", "The cloud service gave an empty answer.");
        }
        catch (Exception ex) when (ex is JSException or JsonException)
        {
            return Fail("provider", ex.Message);
        }
    }

    private static CloudResult Fail(string code, string message) =>
        new() { Ok = false, Code = code, Message = message };

    private void ApplyStatus(CloudStatus? status)
    {
        if (status is not null)
            Status = status;
    }

    private void Notify() => Changed?.Invoke();

    public void Dispose()
    {
        _timer?.Cancel();
        _storage.Persisted -= OnPersisted;
    }
}
