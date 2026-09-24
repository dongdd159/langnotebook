namespace LangNotebook.Models;

/// <summary>Nhat ky moi lan on tap - nguon du lieu cho trang thong ke.</summary>
public class ReviewLog
{
    public int Id { get; set; }

    public int NotebookId { get; set; }

    public StudyItemKind ItemKind { get; set; }

    public int ItemId { get; set; }

    public ReviewGrade Grade { get; set; }

    public DateTime ReviewedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Khoang lap sau khi cham diem (ngay).</summary>
    public int IntervalAfterDays { get; set; }

    public double EaseAfter { get; set; }

    public int DurationMs { get; set; }
}
