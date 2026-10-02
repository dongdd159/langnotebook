using LangNotebook.Dto;
using LangNotebook.Models;

namespace LangNotebook.Service;

public static class SrsScheduler
{
    public const double MinEaseFactor = 1.3;
    public const int MaxIntervalDays = 3650;

    public static SrsOutcome Compute(double easeFactor, int intervalDays, int repetitions, int lapses,
        ReviewGrade grade, DateTime nowUtc)
    {
        if (grade == ReviewGrade.Again)
        {
            var loweredEase = Math.Max(MinEaseFactor, Math.Round(easeFactor - 0.2, 3));
            return new SrsOutcome(loweredEase, 0, 0, lapses + 1, nowUtc.AddMinutes(10));
        }
        var quality = grade switch
        {
            ReviewGrade.Hard => 3, 
            ReviewGrade.Good => 4, 
            _ => 5
        };
        var newEase = easeFactor + (0.1 - (5 - quality) * (0.08 + (5 - quality) * 0.02));
        newEase = Math.Max(MinEaseFactor, Math.Round(newEase, 3));
        int newInterval;
        if (repetitions == 0)
            newInterval = grade == ReviewGrade.Easy ? 3 : 1;
        else if (repetitions == 1)
        {
            newInterval = grade switch
            {
                ReviewGrade.Hard => 3, 
                ReviewGrade.Good => 6, 
                _ => 8
            };
        }
        else
        {
            var factor = grade switch
            {
                ReviewGrade.Hard => 1.2, 
                ReviewGrade.Good => newEase,
                _ => newEase * 1.3
            };
            var baseInterval = Math.Max(1, intervalDays);
            newInterval = (int)Math.Ceiling(baseInterval * factor);
        }
        newInterval = Math.Clamp(newInterval, 1, MaxIntervalDays);
        var due = nowUtc.Date.AddDays(newInterval).AddHours(4);
        return new SrsOutcome(newEase, newInterval, repetitions + 1, lapses, due);
    }
    
    public static SrsOutcome Apply(StudyItemBase item, ReviewGrade grade, DateTime nowUtc)
    {
        var outcome = Compute(item.EaseFactor, item.IntervalDays, item.Repetitions, item.Lapses, grade, nowUtc);
        item.EaseFactor = outcome.EaseFactor;
        item.IntervalDays = outcome.IntervalDays;
        item.Repetitions = outcome.Repetitions;
        item.Lapses = outcome.Lapses;
        item.DueUtc = outcome.DueUtc;
        item.LastReviewedUtc = nowUtc;
        item.UpdatedUtc = nowUtc;
        return outcome;
    }
    
    public static string PreviewLabel(StudyItemBase item, ReviewGrade grade, DateTime nowUtc)
    {
        var outcome = Compute(item.EaseFactor, item.IntervalDays, item.Repetitions, item.Lapses, grade, nowUtc);
        return FormatInterval(outcome.IntervalDays);
    }
    
    public static string FormatInterval(int days)
    {
        if (days <= 0)
            return "10 phút";
        if (days == 1)
            return "1 ngày";
        if (days < 30)
            return $"{days} ngày";
        if (days < 365)
        {
            var months = Math.Round(days / 30.0, 1);
            return $"{months:0.#} tháng";
        }
        var years = Math.Round(days / 365.0, 1);
        return $"{years:0.#} năm";
    }
    
    public static string GradeLabel(ReviewGrade grade) => grade switch
    {
        ReviewGrade.Again => "Quên rồi",
        ReviewGrade.Hard => "Khó",
        ReviewGrade.Good => "Được",
        _ => "Dễ"
    };
}