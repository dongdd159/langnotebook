using LangNotebook.Helper;
using LangNotebook.Service;
using Microsoft.AspNetCore.Components;

namespace LangNotebook.Components;

/// <summary>
/// The cloud backup button in the top bar. The icon's shape (not only its colour) shows the state; the same text is
/// in the label, the tooltip and a polite live region for screen readers.
/// </summary>
public partial class CloudBackupButton : ComponentBase, IDisposable
{
    private string _announcement = string.Empty;
    private CloudSyncState _lastState = CloudSyncState.Disconnected;

    private CloudSyncState State => Cloud.State;
    private bool Working => State == CloudSyncState.Working;
    private bool ShowDot => State is CloudSyncState.Pending or CloudSyncState.DecisionNeeded;

    private string IconName => State switch
    {
        CloudSyncState.Disconnected => "cloud",
        CloudSyncState.Synced => "cloud-check",
        CloudSyncState.Pending => "cloud-up",
        CloudSyncState.Working => "sync",
        CloudSyncState.Offline => "cloud-off",
        CloudSyncState.DecisionNeeded => "cloud",
        _ => "cloud-alert"
    };

    private string ToneClass => State is CloudSyncState.Error or CloudSyncState.NeedsReconnect
        ? "text-nt-warn"
        : "text-mute";

    private string LastBackup => Cloud.Status.LastBackup is { } last ? Labels.RelativeDate(last) : string.Empty;

    private string Label => State switch
    {
        CloudSyncState.Disconnected => "Back up to the cloud",
        CloudSyncState.Working => "Backing up…",
        CloudSyncState.NeedsReconnect => $"Reconnect to {Cloud.Status.ProviderName}",
        CloudSyncState.DecisionNeeded => "Review the backups found in the cloud",
        _ => "Back up now"
    };

    private string Tooltip => State switch
    {
        CloudSyncState.Disconnected => "Back up to the cloud · set up in Settings",
        CloudSyncState.Working => "Backing up…",
        CloudSyncState.Synced when LastBackup.Length > 0 => $"Last backup: {LastBackup} · click to back up now",
        CloudSyncState.Synced => "Click to back up now",
        CloudSyncState.Pending when LastBackup.Length > 0 =>
            $"Changes not backed up yet · last backup: {LastBackup} · click to back up now",
        CloudSyncState.Pending => "Not backed up yet · click to back up now",
        CloudSyncState.Offline => "You're offline · the backup will be sent when you're back online",
        CloudSyncState.NeedsReconnect => $"Sign-in expired · click to reconnect {Cloud.Status.ProviderName}",
        CloudSyncState.DecisionNeeded => $"Choose what to do with the backups found in {Cloud.Status.ProviderName}",
        _ => "The last backup failed · click to try again"
    };

    protected override async Task OnInitializedAsync()
    {
        Cloud.Changed += OnChanged;
        await Cloud.InitializeAsync();
    }

    private async Task OnClickAsync()
    {
        // The button stays focusable while a backup runs (a disabled button would drop keyboard focus), so ignore clicks.
        if (Working)
            return;
        switch (State)
        {
            case CloudSyncState.Disconnected:
            case CloudSyncState.DecisionNeeded:
                Nav.NavigateTo("settings#st-cloud");
                return;
            case CloudSyncState.NeedsReconnect:
                // Signing in again opens a pop-up, so it has to start from this click.
                var connected = await Cloud.ConnectAsync(Cloud.Status.Provider);
                Cloud.Report(connected, CloudAction.Connect);
                if (!connected.Ok)
                    return;
                break;
        }
        Cloud.Report(await Cloud.BackupNowAsync(), CloudAction.Backup);
    }

    private void OnChanged() => InvokeAsync(() =>
    {
        // Say something only when the state changes, so a screen reader isn't told the same thing again.
        if (State != _lastState)
        {
            _lastState = State;
            _announcement = State switch
            {
                CloudSyncState.Working => "Backing up…",
                CloudSyncState.Synced => "Backup complete.",
                CloudSyncState.Offline => "Offline. The backup will be sent when you're back online.",
                CloudSyncState.NeedsReconnect => "Cloud sign-in expired.",
                CloudSyncState.Error => "The last backup failed.",
                _ => string.Empty
            };
        }
        StateHasChanged();
    });

    public void Dispose() => Cloud.Changed -= OnChanged;
}
