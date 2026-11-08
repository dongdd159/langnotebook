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

// Every endpoint here answers a browser CORS preflight; the app has no backend to relay requests through.
public static class AiProviders
{
    public const string AnthropicId = "anthropic";
    public const string CustomId = "custom";
    public const string AnthropicDefaultModel = "claude-opus-5-5";

    public static readonly IReadOnlyList<AiProvider> All =
    [
        new(AnthropicId, "Anthropic (Claude)", AiProtocol.Anthropic, "https://api.anthropic.com/v1/messages",
            AnthropicDefaultModel, AnthropicDefaultModel),
        new("openai", "OpenAI", AiProtocol.OpenAiCompatible, "https://api.openai.com/v1/chat/completions",
            "", "gpt-5"),
        new("gemini", "Google Gemini", AiProtocol.OpenAiCompatible,
            "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions", "", "gemini-2.5-flash"),
        new("openrouter", "OpenRouter", AiProtocol.OpenAiCompatible, "https://openrouter.ai/api/v1/chat/completions",
            "", "openai/gpt-5"),
        new("groq", "Groq", AiProtocol.OpenAiCompatible, "https://api.groq.com/openai/v1/chat/completions",
            "", "llama-3.3-70b-versatile"),
        new("mistral", "Mistral", AiProtocol.OpenAiCompatible, "https://api.mistral.ai/v1/chat/completions",
            "", "mistral-large-latest"),
        new("deepseek", "DeepSeek", AiProtocol.OpenAiCompatible, "https://api.deepseek.com/chat/completions",
            "", "deepseek-chat"),
        new("xai", "xAI (Grok)", AiProtocol.OpenAiCompatible, "https://api.x.ai/v1/chat/completions",
            "", "grok-4"),
        new("together", "Together AI", AiProtocol.OpenAiCompatible, "https://api.together.xyz/v1/chat/completions",
            "", "meta-llama/Llama-3.3-70B-Instruct-Turbo"),
        new(CustomId, "Custom endpoint", AiProtocol.OpenAiCompatible, "", "", "the model name your server expects",
            KeyRequired: false)
    ];

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
