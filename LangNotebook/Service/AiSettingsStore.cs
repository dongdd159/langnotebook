using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace LangNotebook.Service;

public sealed class AiProfile
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
}

public sealed class AiSettings
{
    public string ProviderId { get; set; } = AiProviders.AnthropicId;

    // One profile per provider, so switching back doesn't mean pasting a key again.
    public Dictionary<string, AiProfile> Profiles { get; set; } = new();

    public AiProfile ProfileFor(string providerId)
    {
        if (!Profiles.TryGetValue(providerId, out var profile))
            Profiles[providerId] = profile = new AiProfile();
        return profile;
    }
}

[JsonSerializable(typeof(AiSettings))]
internal partial class AiSettingsJsonContext : JsonSerializerContext
{
}

// Kept in localStorage rather than the SQLite settings table so keys never end up in a JSON or .db backup.
public sealed class AiSettingsStore(IJSRuntime js)
{
    private const string StorageKey = "lang-notebook.ai-settings";
    private const string LegacyAnthropicKey = "lang-notebook.anthropic-api-key";

    public async Task<AiSettings> LoadAsync()
    {
        var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                return JsonSerializer.Deserialize(json, AiSettingsJsonContext.Default.AiSettings) ?? new AiSettings();
            }
            catch (JsonException)
            {
                return new AiSettings();
            }
        }

        var settings = new AiSettings();
        var legacyKey = await js.InvokeAsync<string?>("localStorage.getItem", LegacyAnthropicKey);
        if (string.IsNullOrEmpty(legacyKey))
            return settings;
        settings.ProfileFor(AiProviders.AnthropicId).ApiKey = legacyKey;
        await SaveAsync(settings);
        await js.InvokeVoidAsync("localStorage.removeItem", LegacyAnthropicKey);
        return settings;
    }

    public async Task SaveAsync(AiSettings settings) =>
        await js.InvokeVoidAsync("localStorage.setItem", StorageKey,
            JsonSerializer.Serialize(settings, AiSettingsJsonContext.Default.AiSettings));
}
