using System.Text.Json.Serialization;

namespace LangNotebook.Dto;

public enum AiChangeDecision
{
    Pending,
    Accepted,
    Skipped
}

public sealed class WritingAiCriterion
{
    public const int MaxScore = 25;

    public string Name { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Comment { get; set; } = string.Empty;
}

public sealed class WritingAiChange
{
    public int Start { get; set; }
    public string Original { get; set; } = string.Empty;
    public string Improved { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public AiChangeDecision Decision { get; set; } = AiChangeDecision.Pending;
}

public sealed class WritingAiReview
{
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    // The exact text that was reviewed. Change offsets are relative to this, not to the editor.
    public string SourceText { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<WritingAiCriterion> Criteria { get; set; } = new();
    public List<string> Strengths { get; set; } = new();
    public List<string> Improvements { get; set; } = new();
    public List<WritingAiChange> Changes { get; set; } = new();

    // Suggestions dropped because their quoted text could not be found in the article.
    public int UnmatchedChanges { get; set; }

    [JsonIgnore]
    public int TotalScore => Criteria.Sum(x => x.Score);
}
