using LangNotebook.Dto;
using LangNotebook.Models;

namespace LangNotebook.Service;

public readonly record struct SchedulerOptions(double DesiredRetention)
{
    public static SchedulerOptions Default => new(FsrsScheduler.DefaultRetention);

    public static SchedulerOptions From(AppSettings? settings) => settings is null
        ? Default
        : new SchedulerOptions(
            Math.Clamp(settings.DesiredRetention, FsrsScheduler.MinRetention, FsrsScheduler.MaxRetention));
}

/// <summary>
/// Schedules a review with FSRS and keeps IntervalDays, DueUtc, Lapses and Repetitions up to date, so queues,
/// stages and statistics only need those fields.
/// </summary>
public static class ReviewScheduler
{
    public static SrsOutcome Compute(StudyItemBase item, ReviewGrade grade, DateTime nowUtc, SchedulerOptions options)
    {
        var isNew = item.Repetitions == 0;
        var (stability, difficulty) = MemoryStateOf(item);
        var elapsed = item.LastReviewedUtc is { } last
            ? Math.Max(0, (nowUtc - last).TotalDays)
            : item.IntervalDays;
        var result = FsrsScheduler.Next(isNew, stability, difficulty, elapsed, grade, options.DesiredRetention);
        var due = DueFor(nowUtc, result.IntervalDays);
        // A first "Again" on a new card is not a lapse. Repetitions always grows so a forgotten card never counts as new.
        var lapses = grade == ReviewGrade.Again && !isNew ? item.Lapses + 1 : item.Lapses;
        return new SrsOutcome(result.IntervalDays, item.Repetitions + 1, lapses, due, result.Stability,
            result.Difficulty);
    }

    public static SrsOutcome Apply(StudyItemBase item, ReviewGrade grade, DateTime nowUtc, SchedulerOptions options)
    {
        var outcome = Compute(item, grade, nowUtc, options);
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
        FormatInterval(Compute(item, grade, nowUtc, options).IntervalDays);

    public static DateTime DueFor(DateTime fromUtc, int intervalDays) => intervalDays <= 0
        ? fromUtc.AddMinutes(10)
        : fromUtc.Date.AddDays(intervalDays).AddHours(4);

    /// <summary>
    /// The card's FSRS state. A card with reviews but no state (restored from an older backup) gets an estimate:
    /// its interval is treated as the days to 90% recall, at average difficulty. "Recalculate" in Settings
    /// replaces the estimate with a state replayed from the review log.
    /// </summary>
    public static (double Stability, double Difficulty) MemoryStateOf(StudyItemBase item)
    {
        if (item.Stability > 0)
            return (item.Stability, item.Difficulty < 1 ? AverageDifficulty : item.Difficulty);
        return (Math.Max(item.IntervalDays, 0.4), AverageDifficulty);
    }

    public const double AverageDifficulty = 5.3;

    public static string FormatInterval(int days)
    {
        if (days <= 0)
            return "10 min";
        if (days == 1)
            return "1 day";
        if (days < 30)
            return $"{days} days";
        if (days < 365)
        {
            var months = Math.Round(days / 30.0, 1);
            return $"{months:0.#} mo";
        }
        var years = Math.Round(days / 365.0, 1);
        return $"{years:0.#} yr";
    }

    public static string GradeLabel(ReviewGrade grade) => grade switch
    {
        ReviewGrade.Again => "Again",
        ReviewGrade.Hard => "Hard",
        ReviewGrade.Good => "Good",
        _ => "Easy"
    };
}
