using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LangNotebook.Models;

public class WritingPiece
{
    public int Id { get; set; }
    public int NotebookId { get; set; }
    public int? PromptId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public WritingStatus Status { get; set; } = WritingStatus.Draft;
    public int WordCount { get; set; }
    public int TargetWordCount { get; set; } = 150;
    public int DurationSeconds { get; set; }
    public int AutoScore { get; set; }
    public int ScoreTask { get; set; }
    public int ScoreCoherence { get; set; }
    public int ScoreVocabulary { get; set; }
    public int ScoreGrammar { get; set; }
    public string SelfNotes { get; set; } = string.Empty;
    public string UsedTerms { get; set; } = string.Empty;
    public string AiReviewJson { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    [NotMapped]
    [JsonIgnore]
    public int RubricTotal => ScoreTask + ScoreCoherence + ScoreVocabulary + ScoreGrammar;
    [NotMapped]
    [JsonIgnore]
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? "(untitled)" : Title;
}