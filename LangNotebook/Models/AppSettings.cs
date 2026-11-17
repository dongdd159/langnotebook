namespace LangNotebook.Models;

public class AppSettings
{
    public int Id { get; set; } = 1;
    public int DailyReviewGoal { get; set; } = 30;
    public int NewCardsPerSession { get; set; } = 10;
    public int MaxCardsPerSession { get; set; } = 40;
    public ReviewDirection DefaultDirection { get; set; } = ReviewDirection.TermToMeaning;
    public int? ActiveNotebookId { get; set; }
    public int WeeklyWritingGoal { get; set; } = 600;
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>FSRS only: the recall probability a card should have on the day it is due.</summary>
    public double DesiredRetention { get; set; } = 0.9;
}
