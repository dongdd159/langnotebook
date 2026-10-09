using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LangNotebook.Models;

public abstract class StudyItemBase
{
    public int Id { get; set; }
    public int NotebookId { get; set; }
    public string Tags { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public bool IsSuspended { get; set; }
    public double EaseFactor { get; set; } = 2.5;
    public int IntervalDays { get; set; }
    public int Repetitions { get; set; }
    public int Lapses { get; set; }
    public DateTime? LastReviewedUtc { get; set; }
    public DateTime DueUtc { get; set; } = DateTime.UtcNow;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    [NotMapped]
    [JsonIgnore]
    public LearningStage Stage
    {
        get
        {
            if (Repetitions == 0) return LearningStage.New;
            if (IntervalDays < 1) return LearningStage.Learning;
            if (IntervalDays < 21) return LearningStage.Young;
            return LearningStage.Mature;
        }
    }
    [NotMapped]
    [JsonIgnore]
    public string[] TagList => Tags
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
