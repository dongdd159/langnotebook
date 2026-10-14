using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace LangNotebook.Pages.Setting;

public partial class Settings : ComponentBase
{
    private Models.AppSettings _settings = new();
    private bool _replaceOnImport = true;
    private string _importMessage = string.Empty;

    protected override async Task OnInitializedAsync() => _settings = await SettingsSvc.GetAsync();

    private void OnDirectionChanged(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var value))
            _settings.DefaultDirection = (Models.ReviewDirection)value;
    }

    private void OnReplaceChanged(ChangeEventArgs args) => _replaceOnImport = args.Value is bool b && b;

    private async Task SaveSettingsAsync()
    {
        await SettingsSvc.SaveAsync(_settings);
        State.ShowSuccess("Settings saved.");
        State.NotifyChanged();
    }

    private async Task ExportJsonAsync()
    {
        await Backup.DownloadJsonAsync();
        State.ShowSuccess("A JSON backup file has been created.");
    }

    private async Task ExportDbAsync()
    {
        await Backup.DownloadDatabaseAsync();
        State.ShowSuccess("A .db backup file has been created.");
    }

    private async Task OnFileSelected(InputFileChangeEventArgs args)
    {
        var file = args.File;
        if (file is null)
            return;
        try
        {
            const long maxSize = 64L * 1024 * 1024;
            await using var stream = file.OpenReadStream(maxSize);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            var bytes = memory.ToArray();
            if (file.Name.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            {
                await Backup.RestoreDatabaseAsync(bytes);
                _importMessage = "The database has been restored. Please reload the page to see the new data.";
            }
            else
            {
                var json = System.Text.Encoding.UTF8.GetString(bytes);
                var count = await Backup.ImportJsonAsync(json, _replaceOnImport);
                _importMessage = $"{count} records have been loaded from the JSON file.";
            }
            State.ShowSuccess("Restoration complete.");
            State.NotifyChanged();
        }
        catch (Exception ex)
        {
            _importMessage = $"Unable to read the file: {ex.Message}";
            State.ShowError("Restore Failed.");
        }
    }

    private async Task WipeAsync()
    {
        var confirmed = await JS.InvokeAsync<bool>("confirm",
            args: ["Do you want to delete all learning data from this device? Be sure to back it up first."]);
        if (!confirmed)
            return;
        await Backup.WipeAsync();
        _settings = await SettingsSvc.GetAsync();
        State.ActiveNotebookId = null;
        State.ShowSuccess("Data has been completely erased.");
        State.NotifyChanged();
    }
}