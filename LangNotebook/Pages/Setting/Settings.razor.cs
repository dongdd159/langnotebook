using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace LangNotebook.Pages.Setting;

public partial class Settings : ComponentBase
{
    private Models.AppSettings _settings = new();
    private bool _replaceOnImport = true;
    private string _importMessage = string.Empty;

    private Service.AiSettings _aiSettings = new();
    private string _aiProviderId = Service.AiProviders.AnthropicId;
    private string _aiModel = string.Empty;
    private string _aiBaseUrl = string.Empty;
    private string _apiKeyInput = string.Empty;

    private Service.AiProvider SelectedProvider => Service.AiProviders.Find(_aiProviderId);
    private bool IsCustomProvider => _aiProviderId == Service.AiProviders.CustomId;
    private bool HasSavedApiKey => !string.IsNullOrEmpty(_aiSettings.ProfileFor(_aiProviderId).ApiKey);

    private string ApiKeyStatus => HasSavedApiKey
        ? "A key is saved on this device"
        : SelectedProvider.KeyRequired ? "No key saved yet" : "Optional for servers that don't need one";

    private bool CanSaveAiSettings =>
        !string.IsNullOrWhiteSpace(_aiModel)
        && Service.AiProviders.ResolveEndpoint(SelectedProvider, _aiBaseUrl) is not null
        && (!SelectedProvider.KeyRequired || HasSavedApiKey || !string.IsNullOrWhiteSpace(_apiKeyInput));

    protected override async Task OnInitializedAsync()
    {
        _settings = await SettingsSvc.GetAsync();
        _aiSettings = await AiSettings.LoadAsync();
        ShowAiProvider(Service.AiProviders.Find(_aiSettings.ProviderId).Id);
    }

    private static readonly (string Value, string Label)[] ThemeChoices =
        [("light", "Light"), ("dark", "Dark"), ("system", "System")];

    private string _themeChoice = "system";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;
        _themeChoice = await JS.InvokeAsync<string>("langTheme.stored");
        StateHasChanged();
    }

    private async Task SetThemeAsync(string choice)
    {
        if (choice == "system")
            await JS.InvokeAsync<string>("langTheme.useSystem");
        else
            await JS.InvokeAsync<string>("langTheme.set", choice);
        _themeChoice = choice;
    }

    private void OnAiProviderChanged(ChangeEventArgs args) =>
        ShowAiProvider(Service.AiProviders.Find(args.Value?.ToString()).Id);

    private void ShowAiProvider(string providerId)
    {
        _aiProviderId = providerId;
        var profile = _aiSettings.ProfileFor(providerId);
        _aiModel = string.IsNullOrWhiteSpace(profile.Model) ? SelectedProvider.DefaultModel : profile.Model;
        _aiBaseUrl = profile.BaseUrl;
        _apiKeyInput = string.Empty;
    }

    private async Task SaveAiSettingsAsync()
    {
        var profile = _aiSettings.ProfileFor(_aiProviderId);
        profile.Model = _aiModel.Trim();
        profile.BaseUrl = _aiBaseUrl.Trim();
        if (!string.IsNullOrWhiteSpace(_apiKeyInput))
            profile.ApiKey = _apiKeyInput.Trim();
        _aiSettings.ProviderId = _aiProviderId;
        await AiSettings.SaveAsync(_aiSettings);
        _apiKeyInput = string.Empty;
        State.ShowSuccess("AI settings saved on this device.");
    }

    private async Task RemoveApiKeyAsync()
    {
        _aiSettings.ProfileFor(_aiProviderId).ApiKey = string.Empty;
        await AiSettings.SaveAsync(_aiSettings);
        State.ShowSuccess("API key removed.");
    }

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