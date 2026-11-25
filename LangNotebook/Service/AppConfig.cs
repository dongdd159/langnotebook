using System.Text.Json;
using System.Text.Json.Serialization;

namespace LangNotebook.Service;

/// <summary>
/// Values that differ between deployments, read from wwwroot/config: <c>app.config.example.json</c> (committed, every
/// default) and <c>app.config.json</c> (git-ignored, this deployment's own values). A top-level section in the second
/// file replaces the same section of the first. The JavaScript side reads the same files (wwwroot/js/config.js).
/// Everything in them is sent to the browser with the app, so it is configuration, not secrets.
/// </summary>
public sealed class AppConfig
{
    private const string DefaultsFile = "config/app.config.example.json";
    private const string LocalFile = "config/app.config.json";

    public AiConfig Ai { get; set; } = new();

    public static async Task<AppConfig> LoadAsync(HttpClient http)
    {
        var defaults = await ReadAsync(http, DefaultsFile)
                       ?? throw new InvalidOperationException(
                           $"{DefaultsFile} is missing or isn't valid JSON, so the app can't start. It ships with the app.");
        var local = await ReadAsync(http, LocalFile);
        var config = new AppConfig { Ai = local?.Ai.Providers.Count > 0 ? local.Ai : defaults.Ai };
        config.Ai.Validate();
        return config;
    }

    // Null when the file is missing, unreadable, or not JSON (some hosts answer a missing file with the index page).
    private static async Task<AppConfig?> ReadAsync(HttpClient http, string file)
    {
        try
        {
            var json = await http.GetStringAsync(file);
            return JsonSerializer.Deserialize(json, AppConfigJsonContext.Default.AppConfig);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            return null;
        }
    }
}

public sealed class AiConfig
{
    public AnthropicConfig Anthropic { get; set; } = new();
    public List<AiProviderConfig> Providers { get; set; } = [];

    internal void Validate()
    {
        if (Providers.Count == 0)
            throw new InvalidOperationException("The \"ai.providers\" list in the configuration is empty.");
        if (string.IsNullOrWhiteSpace(Anthropic.ApiVersion))
            throw new InvalidOperationException("The \"ai.anthropic.apiVersion\" value in the configuration is empty.");
        foreach (var required in new[] { AiProviders.AnthropicId, AiProviders.CustomId })
            if (Providers.All(x => x.Id != required))
                throw new InvalidOperationException($"The \"ai.providers\" list in the configuration has no \"{required}\" entry.");
    }
}

public sealed class AnthropicConfig
{
    public string ApiVersion { get; set; } = string.Empty;
    public string BetaHeader { get; set; } = string.Empty;
}

public sealed class AiProviderConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>"anthropic" or "openai" (any OpenAI-compatible chat completions API).</summary>
    public string Protocol { get; set; } = "openai";

    public string Endpoint { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = string.Empty;
    public string ModelHint { get; set; } = string.Empty;
    public bool KeyRequired { get; set; } = true;
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(AppConfig))]
internal partial class AppConfigJsonContext : JsonSerializerContext
{
}
