namespace LangNotebook.Service;

public enum AiProtocol
{
    Anthropic,
    OpenAiCompatible
}

public sealed record AiProvider(
    string Id,
    string Name,
    AiProtocol Protocol,
    string Endpoint,
    string DefaultModel,
    string ModelHint,
    bool KeyRequired = true);

// The providers (endpoints, default models, hints) and the Anthropic API headers are configuration: the built-in
// defaults are in AiConfig.CreateDefaults, and wwwroot/config/app.config.json can replace them. Every endpoint listed there must answer a browser CORS preflight, because
// the app has no backend to relay requests through.
public static class AiProviders
{
    public const string AnthropicId = "anthropic";
    public const string CustomId = "custom";

    public static IReadOnlyList<AiProvider> All { get; private set; } = [];
    public static string AnthropicApiVersion { get; private set; } = string.Empty;
    public static string AnthropicBetaHeader { get; private set; } = string.Empty;
    public static string AnthropicDefaultModel => Find(AnthropicId).DefaultModel;

    /// <summary>Applies the configuration. Called once at start-up, before any provider is looked up.</summary>
    public static void Configure(AiConfig config)
    {
        All = config.Providers.Select(x => new AiProvider(x.Id, x.Name,
            string.Equals(x.Protocol, "anthropic", StringComparison.OrdinalIgnoreCase)
                ? AiProtocol.Anthropic
                : AiProtocol.OpenAiCompatible,
            x.Endpoint, x.DefaultModel, x.ModelHint, x.KeyRequired)).ToList();
        AnthropicApiVersion = config.Anthropic.ApiVersion;
        AnthropicBetaHeader = config.Anthropic.BetaHeader;
    }

    public static AiProvider Find(string? id) => All.FirstOrDefault(x => x.Id == id) ?? All[0];

    // Accepts either a base URL (".../v1") or the full chat completions URL. Returns null when it isn't usable.
    public static string? ResolveEndpoint(AiProvider provider, string baseUrl)
    {
        if (provider.Id != CustomId)
            return provider.Endpoint;
        var url = baseUrl.Trim().TrimEnd('/');
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return null;
        return url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase) ? url : $"{url}/chat/completions";
    }
}
