using LangNotebook.Models;

namespace LangNotebook.Dto;

public sealed class DashboardStats
{
    public int TotalVocab { get; init; }
    public int TotalGrammar { get; init; }
    public int NewCount { get; init; }
    public int LearningCount { get; init; }
    public int YoungCount { get; init; }
    public int MatureCount { get; init; }
    public int DueNow { get; init; }
    public int ReviewsToday { get; init; }
    public int ReviewsLast7Days { get; init; }
    public int ReviewsTotal { get; init; }
    public double AccuracyLast30Days { get; init; }
    public double AverageSecondsPerCard { get; init; }
    public int StreakDays { get; init; }
    public int LongestStreak { get; init; }
    public int PiecesTotal { get; init; }
    public int WordsWrittenTotal { get; init; }
    public int WordsWrittenThisWeek { get; init; }
    public double AverageAutoScore { get; init; }
    public List<DayCount> DailyReviews { get; init; } = new();
    public List<DayCount> Heatmap { get; init; } = new();
    public List<NotebookSummary> Notebooks { get; init; } = new();
    public Dictionary<ReviewGrade, int> GradeBreakdown { get; init; } = new();
}
