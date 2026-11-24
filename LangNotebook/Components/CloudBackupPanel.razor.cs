using LangNotebook.Helper;
using LangNotebook.Service;
using Microsoft.AspNetCore.Components;

namespace LangNotebook.Components;

public partial class CloudBackupPanel : ComponentBase, IDisposable
{
    private string? _connectingId;
    private bool _decisionOpen;
    private CloudFile? _restoreTarget;

    private CloudFile? Latest => Cloud.Files.FirstOrDefault();

    private string DecisionTitle => $"Backups found in {Cloud.Status.ProviderName}";

    private string AccountLine
    {
        get
        {
            var account = Cloud.Status.Account.Length > 0 ? $"Signed in as {Cloud.Status.Account}" : "Signed in";
            return $"{account} · This device: {Cloud.Status.DeviceLabel}";
        }
    }

    private string LastBackupLine => Cloud.Status.LastBackup is { } last
        ? $"Last backup: {Labels.RelativeDate(last)}"
        : "No backup from this device yet";

    private string StatusDetail => Cloud.State switch
    {
        CloudSyncState.Working => "Working…",
        CloudSyncState.Pending => "Changes not backed up yet",
        CloudSyncState.Offline => "You're offline. The backup will be sent when you're back online.",
        CloudSyncState.NeedsReconnect => "Your sign-in has expired. Connect again to keep backing up.",
        CloudSyncState.DecisionNeeded => "Choose what to do with the backups found in the cloud first.",
        CloudSyncState.Error => $"The last backup failed. {Cloud.LastErrorMessage}".TrimEnd(),
        _ => "Everything is backed up"
    };

    // Each is built as one string so it can be translated as a whole sentence.
    private static string LatestLine(CloudFile file) =>
        $"The latest backup is from {Labels.RelativeDate(file.Created)} on {DeviceLine(file)}. What should this device do?";

    private static string RestoreLine(CloudFile file) =>
        $"This replaces everything on this device with the backup from {Labels.RelativeDate(file.Created)} ({DeviceLine(file)}). Your current data is saved first, so you can undo this.";

    private static string DeviceLine(CloudFile file) =>
        file.ThisDevice ? $"{file.DeviceLabel} (this device)" : file.DeviceLabel;

    protected override async Task OnInitializedAsync()
    {
        Cloud.Changed += OnChanged;
        await Cloud.InitializeAsync();
        if (Cloud.Status.Connected && Cloud.Files.Count == 0)
            await Cloud.RefreshFilesAsync();
        _decisionOpen = Cloud.State == CloudSyncState.DecisionNeeded && Cloud.Files.Count > 0;
    }

    private async Task ConnectAsync(string providerId)
    {
        _connectingId = providerId;
        // The sign-in window opens from this call, so nothing may be awaited before it.
        var result = await Cloud.ConnectAsync(providerId);
        _connectingId = null;
        Cloud.Report(result, CloudAction.Connect);
        if (result.Ok && Cloud.State == CloudSyncState.DecisionNeeded)
            _decisionOpen = true;
    }

    private async Task CancelConnectAsync() => await Cloud.CancelConnectAsync();

    private async Task DisconnectAsync() => Cloud.Report(await Cloud.DisconnectAsync(), CloudAction.Disconnect);

    private async Task BackupAsync() => Cloud.Report(await Cloud.BackupNowAsync(), CloudAction.Backup);

    private async Task RefreshAsync() => await Cloud.RefreshFilesAsync();

    private async Task OnAutoBackupChanged(ChangeEventArgs args) =>
        await Cloud.SetAutoBackupAsync(args.Value is bool enabled && enabled);

    private void OpenDecision() => _decisionOpen = true;

    private void CloseDecision() => _decisionOpen = false;

    private void OnDecisionVisibleChanged(bool visible) => _decisionOpen = visible;

    private async Task RestoreLatestAsync()
    {
        if (Latest is not { } latest)
            return;
        _decisionOpen = false;
        Cloud.Report(await Cloud.RestoreAsync(latest.Id), CloudAction.Restore);
    }

    private async Task KeepLocalAsync()
    {
        _decisionOpen = false;
        await Cloud.SetArmedAsync(true);
        Cloud.Report(await Cloud.BackupNowAsync(true), CloudAction.Backup);
    }

    private void AskRestore(CloudFile file) => _restoreTarget = file;

    private void CloseRestore() => _restoreTarget = null;

    private void OnRestoreVisibleChanged(bool visible)
    {
        if (!visible)
            _restoreTarget = null;
    }

    private async Task ConfirmRestoreAsync()
    {
        if (_restoreTarget is not { } target)
            return;
        _restoreTarget = null;
        Cloud.Report(await Cloud.RestoreAsync(target.Id), CloudAction.Restore);
    }

    private async Task UndoAsync() => Cloud.Report(await Cloud.UndoRestoreAsync(), CloudAction.Undo);

    private void OnChanged() => InvokeAsync(StateHasChanged);

    public void Dispose() => Cloud.Changed -= OnChanged;
}
