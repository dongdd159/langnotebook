using LangNotebook.Dto;
using LangNotebook.Models;

namespace LangNotebook.Service;

public readonly record struct SchedulerOptions(SchedulerAlgorithm Algorithm, double DesiredRetention)
{
    public static SchedulerOptions Default => new(SchedulerAlgorithm.Sm2, FsrsScheduler.DefaultRetention);

    public static SchedulerOptions From(AppSettings? settings) => settings is null
        ? Default
        : new SchedulerOptions(settings.Algorithm,
            Math.Clamp(settings.DesiredRetention, FsrsScheduler.MinRetention, FsrsScheduler.MaxRetention));
}

/// <summary>Picks SM-2 or FSRS for a review. Both keep IntervalDays, DueUtc, Lapses and Repetitions up to date.</summary>
public static class ReviewScheduler
{
    public static SrsOutcome Compute(StudyItemBase item, ReviewGrade grade, DateTime nowUtc, SchedulerOptions options)
    {
        if (options.Algorithm == SchedulerAlgorithm.Sm2)
        {
            var sm2 = SrsScheduler.Compute(item.EaseFactor, item.IntervalDays, item.Repetitions, item.Lapses, grade,
                nowUtc);
            return sm2 with { Stability = item.Stability, Difficulty = item.Difficulty };
        }
        var isNew = item.Repetitions == 0;
        var (stability, difficulty) = MemoryStateOf(item);
        var elapsed = item.LastReviewedUtc is { } last
            ? Math.Max(0, (nowUtc - last).TotalDays)
            : item.IntervalDays;
        var result = FsrsScheduler.Next(isNew, stability, difficulty, elapsed, grade, options.DesiredRetention);
        var due = DueFor(nowUtc, result.IntervalDays);
        // A first "Again" on a new card is not a lapse. Repetitions always grows so a forgotten card never counts as new.
        var lapses = grade == ReviewGrade.Again && !isNew ? item.Lapses + 1 : item.Lapses;
        return new SrsOutcome(item.EaseFactor, result.IntervalDays, item.Repetitions + 1, lapses, due,
            result.Stability, result.Difficulty);
    }

    public static SrsOutcome Apply(StudyItemBase item, ReviewGrade grade, DateTime nowUtc, SchedulerOptions options)
    {
        var outcome = Compute(item, grade, nowUtc, options);
        item.EaseFactor = outcome.EaseFactor;
        item.IntervalDays = outcome.IntervalDays;
        item.Repetitions = outcome.Repetitions;
        item.Lapses = outcome.Lapses;
        item.DueUtc = outcome.DueUtc;
        item.Stability = outcome.Stability;
        item.Difficulty = outcome.Difficulty;
        item.LastReviewedUtc = nowUtc;
        item.UpdatedUtc = nowUtc;
        return outcome;
    }

    public static string PreviewLabel(StudyItemBase item, ReviewGrade grade, DateTime nowUtc,
        SchedulerOptions options) =>
        SrsScheduler.FormatInterval(Compute(item, grade, nowUtc, options).IntervalDays);

    public static DateTime DueFor(DateTime fromUtc, int intervalDays) => intervalDays <= 0
        ? fromUtc.AddMinutes(10)
        : fromUtc.Date.AddDays(intervalDays).AddHours(4);

    /// <summary>
    /// The card's FSRS state. A card that was only ever scheduled by SM-2 has none, so it is estimated:
    /// its interval is treated as the days to 90% recall, and a low ease factor maps to a high difficulty.
    /// </summary>
    public static (double Stability, double Difficulty) MemoryStateOf(StudyItemBase item)
    {
        if (item.Stability > 0)
            return (item.Stability, item.Difficulty < 1 ? 5.3 : item.Difficulty);
        var stability = Math.Max(item.IntervalDays, 0.4);
        var difficulty = Math.Clamp(5.3 + (2.5 - item.EaseFactor) * 3.5, 1, 10);
        return (stability, difficulty);
    }
}
