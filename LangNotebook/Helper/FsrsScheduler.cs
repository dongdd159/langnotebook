using LangNotebook.Models;

namespace LangNotebook.Service;

/// <summary>The memory state and next interval FSRS computes for one review.</summary>
/// <param name="IntervalDays">0 means "again in a few minutes"; otherwise whole days, at least 1.</param>
public readonly record struct FsrsResult(double Stability, double Difficulty, int IntervalDays);

/// <summary>
/// FSRS-5 with the published default weights. A card's memory is described by Difficulty (1-10) and
/// Stability (days for recall probability to drop to 90%). Recall probability on a given day is derived from them,
/// and the next review is scheduled for the day it falls to the desired retention.
/// </summary>
public static class FsrsScheduler
{
    public const double Decay = -0.5;

    // Chosen so that Retrievability(S, S) = 0.9, which makes Stability mean "days to 90%".
    public const double Factor = 19.0 / 81.0;
    public const int MaxIntervalDays = 3650;
    public const double MinStability = 0.01;
    public const double MaxStability = 36500;
    public const double MinRetention = 0.7;
    public const double MaxRetention = 0.97;
    public const double DefaultRetention = 0.9;

    private static readonly double[] W =
    [
        0.40255, 1.18385, 3.173, 15.69105, 7.1949, 0.5345, 1.4604, 0.0046, 1.54575, 0.1192,
        1.01925, 1.9395, 0.11, 0.29605, 2.2698, 0.2315, 2.9898, 0.51655, 0.6621
    ];

    /// <summary>Probability of recalling a card <paramref name="elapsedDays"/> after its last review.</summary>
    public static double Retrievability(double stability, double elapsedDays)
    {
        if (stability <= 0)
            return 0;
        return Math.Pow(1 + Factor * Math.Max(0, elapsedDays) / stability, Decay);
    }

    /// <summary>Whole days until recall probability falls to <paramref name="retention"/>.</summary>
    public static int IntervalFor(double stability, double retention)
    {
        retention = Math.Clamp(retention, MinRetention, MaxRetention);
        var days = stability / Factor * (Math.Pow(retention, 1 / Decay) - 1);
        return (int)Math.Clamp(Math.Round(days), 1, MaxIntervalDays);
    }

    public static FsrsResult Next(bool isNew, double stability, double difficulty, double elapsedDays,
        ReviewGrade grade, double retention)
    {
        var rating = (int)grade + 1; // Again 1, Hard 2, Good 3, Easy 4
        double newStability, newDifficulty;
        if (isNew || stability <= 0)
        {
            newStability = W[rating - 1];
            newDifficulty = InitialDifficulty(rating);
        }
        else
        {
            newDifficulty = NextDifficulty(difficulty, rating);
            if (elapsedDays < 1)
                newStability = SameDayStability(stability, grade, rating);
            else
            {
                var recall = Retrievability(stability, elapsedDays);
                newStability = grade == ReviewGrade.Again
                    ? StabilityAfterForgetting(stability, difficulty, recall)
                    : StabilityAfterRecall(stability, difficulty, recall, grade);
            }
        }
        newStability = Math.Clamp(newStability, MinStability, MaxStability);
        var interval = grade == ReviewGrade.Again ? 0 : IntervalFor(newStability, retention);
        return new FsrsResult(newStability, newDifficulty, interval);
    }

    private static double InitialDifficulty(int rating) =>
        Math.Clamp(W[4] - Math.Exp(W[5] * (rating - 1)) + 1, 1, 10);

    private static double NextDifficulty(double difficulty, int rating)
    {
        var shifted = difficulty - W[6] * (rating - 3) * (10 - difficulty) / 9;
        // Pull toward the difficulty of a first "Easy" so one bad streak does not mark a card as hard forever.
        var blended = W[7] * InitialDifficulty(4) + (1 - W[7]) * shifted;
        return Math.Clamp(blended, 1, 10);
    }

    private static double StabilityAfterRecall(double s, double d, double r, ReviewGrade grade)
    {
        var hardPenalty = grade == ReviewGrade.Hard ? W[15] : 1;
        var easyBonus = grade == ReviewGrade.Easy ? W[16] : 1;
        var growth = Math.Exp(W[8]) * (11 - d) * Math.Pow(s, -W[9]) * (Math.Exp(W[10] * (1 - r)) - 1)
                     * hardPenalty * easyBonus;
        return s * (1 + growth);
    }

    private static double StabilityAfterForgetting(double s, double d, double r)
    {
        var forgotten = W[11] * Math.Pow(d, -W[12]) * (Math.Pow(s + 1, W[13]) - 1) * Math.Exp(W[14] * (1 - r));
        return Math.Min(forgotten, s);
    }

    // Reviews less than a day apart, such as relearning a card that was just forgotten.
    private static double SameDayStability(double s, ReviewGrade grade, int rating)
    {
        var next = s * Math.Exp(W[17] * (rating - 3 + W[18]));
        return grade is ReviewGrade.Good or ReviewGrade.Easy ? Math.Max(next, s) : next;
    }
}
