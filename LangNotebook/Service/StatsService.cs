using LangNotebook.Data;
using LangNotebook.Dto;
using LangNotebook.Models;
using Microsoft.EntityFrameworkCore;

namespace LangNotebook.Service;

public class StatsService(IDbContextFactory<ApplicationContext> factory, DatabaseInitializer initializer)
{
    private readonly IDbContextFactory<ApplicationContext> _factory = factory;
    private readonly DatabaseInitializer _initializer = initializer;

    public async Task<DashboardStats> GetAsync(int? notebookId, int heatmapWeeks = 12, int trendDays = 30)
    {
        await _initializer.EnsureReadyAsync();
        await using var db = await _factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var today = DateTime.Now.Date;
        var vocab = await db.VocabEntries.AsNoTracking()
            .Where(x => !notebookId.HasValue || x.NotebookId == notebookId.Value).ToListAsync();
        var grammar = await db.GrammarPatterns.AsNoTracking()
            .Where(x => !notebookId.HasValue || x.NotebookId == notebookId.Value).ToListAsync();
        var logs = await db.ReviewLogs.AsNoTracking()
            .Where(x => !notebookId.HasValue || x.NotebookId == notebookId.Value).ToListAsync();
        var pieces = await db.WritingPieces.AsNoTracking()
            .Where(x => !notebookId.HasValue || x.NotebookId == notebookId.Value).ToListAsync();
        var notebooks = await db.Notebooks.AsNoTracking().ToListAsync();
        var allItems = new List<StudyItemBase>();
        allItems.AddRange(vocab);
        allItems.AddRange(grammar);
        var logDays = logs.Select(x => x.ReviewedUtc.ToLocalTime().Date).ToList();
        var byDay = logDays.GroupBy(d => d).ToDictionary(g => g.Key, g => g.Count());
        var writingDays = pieces.Select(x => x.UpdatedUtc.ToLocalTime().Date).Distinct().ToHashSet();
        var activeDays = byDay.Keys.Concat(writingDays).ToHashSet();
        var last30 = logs.Where(x => x.ReviewedUtc >= now.AddDays(-30)).ToList();
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var dailyReviews = Enumerable.Range(0, trendDays).Select(offset =>
        {
            var date = today.AddDays(-(trendDays - 1 - offset));
            return new DayCount { Date = date, Count = byDay.TryGetValue(date, out var c) ? c : 0 };
        }).ToList();
        var heatmapStart = today.AddDays(-(heatmapWeeks * 7 - 1));
        heatmapStart = heatmapStart.AddDays(-(int)heatmapStart.DayOfWeek);
        var heatmap = new List<DayCount>();
        for (var date = heatmapStart; date <= today; date = date.AddDays(1))
            heatmap.Add(new DayCount { Date = date, Count = byDay.TryGetValue(date, out var c) ? c : 0 });
        var summaries = notebooks.Where(n => !notebookId.HasValue || n.Id == notebookId.Value).Select(n =>
                new NotebookSummary
                {
                    NotebookId = n.Id,
                    Name = n.Name,
                    ColorHex = n.ColorHex,
                    VocabCount = vocab.Count(x => x.NotebookId == n.Id),
                    GrammarCount = grammar.Count(x => x.NotebookId == n.Id),
                    MatureCount = allItems.Count(x => x.NotebookId == n.Id && x.Stage == LearningStage.Mature),
                    DueCount = allItems.Count(x => x.NotebookId == n.Id && !x.IsSuspended && x.DueUtc <= now)
                }).Where(s => s.VocabCount + s.GrammarCount > 0).OrderByDescending(s => s.VocabCount + s.GrammarCount)
            .ToList();
        var gradeBreakdown = Enum.GetValues<ReviewGrade>().ToDictionary(g => g, g => logs.Count(x => x.Grade == g));
        return new DashboardStats
        {
            TotalVocab = vocab.Count, TotalGrammar = grammar.Count,
            NewCount = allItems.Count(x => x.Stage == LearningStage.New),
            LearningCount = allItems.Count(x => x.Stage == LearningStage.Learning),
            YoungCount = allItems.Count(x => x.Stage == LearningStage.Young),
            MatureCount = allItems.Count(x => x.Stage == LearningStage.Mature),
            DueNow = allItems.Count(x => !x.IsSuspended && x.Repetitions > 0 && x.DueUtc <= now),
            ReviewsToday = byDay.TryGetValue(today, out var todayCount) ? todayCount : 0,
            ReviewsLast7Days = logs.Count(x => x.ReviewedUtc >= now.AddDays(-7)), ReviewsTotal = logs.Count,
            AccuracyLast30Days = last30.Count == 0
                ? 0
                : (double)last30.Count(x => x.Grade != ReviewGrade.Again) / last30.Count,
            AverageSecondsPerCard = last30.Count == 0
                ? 0
                : Math.Round(last30.Average(x => x.DurationMs) / 1000.0, 1),
            StreakDays = ComputeCurrentStreak(activeDays, today), LongestStreak = ComputeLongestStreak(activeDays),
            PiecesTotal = pieces.Count, WordsWrittenTotal = pieces.Sum(x => x.WordCount),
            WordsWrittenThisWeek =
                pieces.Where(x => x.UpdatedUtc.ToLocalTime().Date >= weekStart).Sum(x => x.WordCount),
            AverageAutoScore = pieces.Count == 0 ? 0 : Math.Round(pieces.Average(x => x.AutoScore), 1),
            DailyReviews = dailyReviews, Heatmap = heatmap, Notebooks = summaries, GradeBreakdown = gradeBreakdown
        };
    }

    private static int ComputeCurrentStreak(HashSet<DateTime> activeDays, DateTime today)
    {
        if (activeDays.Count == 0)
            return 0;
        var cursor = activeDays.Contains(today) ? today : today.AddDays(-1);
        if (!activeDays.Contains(cursor))
            return 0;
        var streak = 0;
        while (activeDays.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        return streak;
    }

    private static int ComputeLongestStreak(HashSet<DateTime> activeDays)
    {
        if (activeDays.Count == 0)
            return 0;
        var ordered = activeDays.OrderBy(x => x).ToList();
        var best = 1;
        var current = 1;
        for (var i = 1; i < ordered.Count; i++)
        {
            if ((ordered[i] - ordered[i - 1]).TotalDays == 1)
            {
                current++;
                best = Math.Max(best, current);
            }
            else
                current = 1;
        }
        return best;
    }
}