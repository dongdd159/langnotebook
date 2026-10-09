namespace LangNotebook.Models;

public class ReviewLog
{
    public int Id { get; set; }

    public int NotebookId { get; set; }

    public StudyItemKind ItemKind { get; set; }

    public int ItemId { get; set; }

    public ReviewGrade Grade { get; set; }

    public DateTime ReviewedUtc { get; set; } = DateTime.UtcNow;

    public int IntervalAfterDays { get; set; }

    public double EaseAfter { get; set; }

    public int DurationMs { get; set; }
}
