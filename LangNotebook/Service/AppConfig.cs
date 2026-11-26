using System.Text.Json;
using System.Text.Json.Serialization;

namespace LangNotebook.Service;

/// <summary>
/// Values that differ between deployments. The built-in defaults below are used as they are; a git-ignored
/// <c>wwwroot/config/app.config.json</c> (the one configuration file, see docs/configuration.md) overrides a whole section
/// of them when it has one. The JavaScript side reads the same file for the cloud client IDs (wwwroot/js/config.js).
/// A missing, unreadable or invalid file is skipped, so the app always starts.
/// Everything in the file is sent to the browser with the app, so it is configuration, not secrets.
/// </summary>
public sealed class AppConfig
{
    private const string ConfigFile = "config/app.config.json";

    public AiConfig Ai { get; set; } = new();

    public static async Task<AppConfig> LoadAsync(HttpClient http)
    {
        var defaults = AiConfig.CreateDefaults();
        var local = await ReadAsync(http);
        var ai = new AiConfig
        {
            // A section in the file replaces the same section of the defaults.
            Anthropic = !string.IsNullOrWhiteSpace(local?.Ai.Anthropic.ApiVersion) ? local.Ai.Anthropic : defaults.Anthropic,
            Providers = local?.Ai.Providers.Count > 0 ? local.Ai.Providers : defaults.Providers
        };
        try
        {
            ai.Validate();
        }
        catch (InvalidOperationException ex)
        {
            // A broken file must not stop the app: say what is wrong and use the defaults.
            Console.Error.WriteLine($"[LangNotebook] Ignoring the AI settings in {ConfigFile}: {ex.Message}");
            ai = defaults;
        }
        return new AppConfig { Ai = ai };
    }

    // Null when the file is missing, unreadable, or not JSON (some hosts answer a missing file with the index page).
    private static async Task<AppConfig?> ReadAsync(HttpClient http)
    {
        try
        {
            var json = await http.GetStringAsync(ConfigFile);
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

    /// <summary>The providers and Anthropic API headers the app ships with.</summary>
    public static AiConfig CreateDefaults() => new()
    {
        Anthropic = new AnthropicConfig { ApiVersion = "2026-10-21", BetaHeader = "server-side-fallback-2026-07-01" },
        Providers =
        [
            new() { Id = "anthropic", Name = "Anthropic (Claude)", Protocol = "anthropic", Endpoint = "https://api.anthropic.com/v1/messages", DefaultModel = "claude-opus-5-5", ModelHint = "claude-opus-5-5" },
            new() { Id = "openai", Name = "OpenAI", Protocol = "openai", Endpoint = "https://api.openai.com/v1/chat/completions", DefaultModel = "", ModelHint = "gpt-5" },
            new() { Id = "gemini", Name = "Google Gemini", Protocol = "openai", Endpoint = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions", DefaultModel = "", ModelHint = "gemini-2.5-flash" },
            new() { Id = "openrouter", Name = "OpenRouter", Protocol = "openai", Endpoint = "https://openrouter.ai/api/v1/chat/completions", DefaultModel = "", ModelHint = "openai/gpt-5" },
            new() { Id = "groq", Name = "Groq", Protocol = "openai", Endpoint = "https://api.groq.com/openai/v1/chat/completions", DefaultModel = "", ModelHint = "llama-3.3-70b-versatile" },
            new() { Id = "mistral", Name = "Mistral", Protocol = "openai", Endpoint = "https://api.mistral.ai/v1/chat/completions", DefaultModel = "", ModelHint = "mistral-large-latest" },
            new() { Id = "deepseek", Name = "DeepSeek", Protocol = "openai", Endpoint = "https://api.deepseek.com/chat/completions", DefaultModel = "", ModelHint = "deepseek-chat" },
            new() { Id = "xai", Name = "xAI (Grok)", Protocol = "openai", Endpoint = "https://api.x.ai/v1/chat/completions", DefaultModel = "", ModelHint = "grok-4" },
            new() { Id = "together", Name = "Together AI", Protocol = "openai", Endpoint = "https://api.together.xyz/v1/chat/completions", DefaultModel = "", ModelHint = "meta-llama/Llama-3.3-70B-Instruct-Turbo" },
            new() { Id = "custom", Name = "Custom endpoint", Protocol = "openai", Endpoint = "", DefaultModel = "", ModelHint = "the model name your server expects", KeyRequired = false }
        ]
    };

    internal void Validate()
    {
        if (Providers.Count == 0)
            throw new InvalidOperationException("The \"ai.providers\" list is empty.");
        if (string.IsNullOrWhiteSpace(Anthropic.ApiVersion))
            throw new InvalidOperationException("The \"ai.anthropic.apiVersion\" value is empty.");
        foreach (var required in new[] { AiProviders.AnthropicId, AiProviders.CustomId })
            if (Providers.All(x => x.Id != required))
                throw new InvalidOperationException($"The \"ai.providers\" list has no \"{required}\" entry.");
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
