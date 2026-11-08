using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LangNotebook.Dto;

namespace LangNotebook.Helper;

public sealed class AiCriterionPayload
{
    // Models without schema enforcement sometimes answer 21.5 or "21".
    [JsonPropertyName("score")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public double Score { get; set; }
    [JsonPropertyName("comment")] public string Comment { get; set; } = string.Empty;
}

public sealed class AiCriteriaPayload
{
    [JsonPropertyName("task_fulfillment")] public AiCriterionPayload Task { get; set; } = new();
    [JsonPropertyName("coherence")] public AiCriterionPayload Coherence { get; set; } = new();
    [JsonPropertyName("vocabulary")] public AiCriterionPayload Vocabulary { get; set; } = new();
    [JsonPropertyName("grammar")] public AiCriterionPayload Grammar { get; set; } = new();
}

public sealed class AiChangePayload
{
    [JsonPropertyName("original")] public string Original { get; set; } = string.Empty;
    [JsonPropertyName("improved")] public string Improved { get; set; } = string.Empty;
    [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;
    [JsonPropertyName("explanation")] public string Explanation { get; set; } = string.Empty;
}

public sealed class AiReviewPayload
{
    [JsonPropertyName("summary")] public string Summary { get; set; } = string.Empty;
    [JsonPropertyName("criteria")] public AiCriteriaPayload Criteria { get; set; } = new();
    [JsonPropertyName("strengths")] public List<string> Strengths { get; set; } = new();
    [JsonPropertyName("improvements")] public List<string> Improvements { get; set; } = new();
    [JsonPropertyName("changes")] public List<AiChangePayload> Changes { get; set; } = new();
}

[JsonSerializable(typeof(AiReviewPayload))]
[JsonSerializable(typeof(WritingAiReview))]
internal partial class AiJsonContext : JsonSerializerContext
{
}

public static class WritingAiReviewMapper
{
    public static WritingAiReview FromModelJson(string json, string sourceText)
    {
        var payload = JsonSerializer.Deserialize(ExtractJsonObject(json), AiJsonContext.Default.AiReviewPayload)
                      ?? throw new JsonException("The review was empty.");
        var (changes, unmatched) = LocateChanges(payload.Changes ?? [], sourceText);
        return new WritingAiReview
        {
            SourceText = sourceText,
            Summary = payload.Summary,
            Criteria =
            [
                ToCriterion("Task fulfillment", payload.Criteria.Task),
                ToCriterion("Coherence & layout", payload.Criteria.Coherence),
                ToCriterion("Vocabulary", payload.Criteria.Vocabulary),
                ToCriterion("Grammar", payload.Criteria.Grammar)
            ],
            Strengths = payload.Strengths ?? [],
            Improvements = payload.Improvements ?? [],
            Changes = changes,
            UnmatchedChanges = unmatched
        };
    }

    // Without schema enforcement a model may wrap the object in a code fence, prose, or a <think> block.
    private static string ExtractJsonObject(string text)
    {
        const string thinkEnd = "</think>";
        var thinkIndex = text.LastIndexOf(thinkEnd, StringComparison.Ordinal);
        var from = thinkIndex < 0 ? 0 : thinkIndex + thinkEnd.Length;
        var start = text.IndexOf('{', from);
        var end = text.LastIndexOf('}');
        if (start < 0 || end < start)
            throw new JsonException("The response had no JSON object.");
        return text[start..(end + 1)];
    }

    private static WritingAiCriterion ToCriterion(string name, AiCriterionPayload payload) => new()
    {
        Name = name,
        Score = Math.Clamp((int)Math.Round(payload.Score), 0, WritingAiCriterion.MaxScore),
        Comment = payload.Comment
    };

    private static string NormalizeCategory(string? category) => category?.Trim().ToLowerInvariant() switch
    {
        "grammar" => "grammar",
        "vocabulary" => "vocabulary",
        _ => "phrasing"
    };

    // The model quotes the text it wants to replace. Offsets come from searching the real article, so a
    // misquoted suggestion is dropped and the student's own words can never be rewritten by accident.
    private static (List<WritingAiChange> Changes, int Unmatched) LocateChanges(
        IEnumerable<AiChangePayload> proposed, string source)
    {
        var located = new List<WritingAiChange>();
        var unmatched = 0;
        var cursor = 0;
        foreach (var item in proposed)
        {
            if (string.IsNullOrEmpty(item.Original))
            {
                unmatched++;
                continue;
            }
            var index = source.IndexOf(item.Original, cursor, StringComparison.Ordinal);
            if (index < 0)
            {
                unmatched++;
                continue;
            }
            located.Add(new WritingAiChange
            {
                Start = index,
                Original = item.Original,
                Improved = item.Improved ?? string.Empty,
                Category = NormalizeCategory(item.Category),
                Explanation = item.Explanation ?? string.Empty
            });
            cursor = index + item.Original.Length;
        }
        return (located, unmatched);
    }

    public static string ApplyChanges(WritingAiReview review, Func<WritingAiChange, bool> include)
    {
        var builder = new StringBuilder();
        var cursor = 0;
        foreach (var change in review.Changes)
        {
            builder.Append(review.SourceText, cursor, change.Start - cursor);
            builder.Append(include(change) ? change.Improved : change.Original);
            cursor = change.Start + change.Original.Length;
        }
        builder.Append(review.SourceText, cursor, review.SourceText.Length - cursor);
        return builder.ToString();
    }

    public static string Serialize(WritingAiReview review) =>
        JsonSerializer.Serialize(review, AiJsonContext.Default.WritingAiReview);

    public static WritingAiReview? Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize(json, AiJsonContext.Default.WritingAiReview);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
