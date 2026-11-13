using LangNotebook.Models;

namespace LangNotebook.Helper;

public static class Labels
{
    public static string Stage(LearningStage stage) => stage switch
    {
        LearningStage.New => "New",
        LearningStage.Learning => "Learning",
        LearningStage.Young => "Young",
        _ => "Mature"
    };

    public static string Level(ProficiencyLevel level) => level switch
    {
        ProficiencyLevel.Beginner => "Beginner",
        ProficiencyLevel.Elementary => "Elementary",
        ProficiencyLevel.Intermediate => "Intermediate",
        ProficiencyLevel.UpperIntermediate => "Upper-Intermediate",
        _ => "Advanced"
    };

    public static string Scope(ReviewScope scope) => scope switch
    {
        ReviewScope.All => "Vocabulary & Grammar",
        ReviewScope.VocabularyOnly => "Vocabulary Only",
        _ => "Grammar Only"
    };

    public static string Kind(StudyItemKind kind) => kind == StudyItemKind.Vocabulary ? "Vocabulary" : "Grammar";

    public static string Status(WritingStatus status) => status == WritingStatus.Draft ? "Draft" : "Completed";

    public static string Due(StudyItemBase item)
    {
        if (item.IsSuspended)
            return "suspended";
        if (item.Repetitions == 0)
            return "not learned";
        var now = DateTime.UtcNow;
        if (item.DueUtc <= now)
            return "due";
        var days = (int)Math.Ceiling((item.DueUtc - now).TotalDays);
        return days <= 1 ? "due today" : $"in {days} days";
    }

    public static string RelativeDate(DateTime utc)
    {
        var local = utc.ToLocalTime();
        var days = (int)(DateTime.Now.Date - local.Date).TotalDays;
        return days switch
        {
            0 => $"Today at {local:HH:mm}",
            1 => $"Yesterday at {local:HH:mm}",
            < 7 => $"{days} days ago",
            _ => local.ToString("MMM d, yyyy")
        };
    }

    public static string Duration(int seconds)
    {
        if (seconds <= 0)
            return "0 mins";
        var span = TimeSpan.FromSeconds(seconds);
        if (span.TotalMinutes < 1)
            return $"{seconds} secs";
        if (span.TotalHours < 1)
            return $"{(int)span.TotalMinutes} mins";
        return $"{(int)span.TotalHours} hrs {span.Minutes} mins";
    }
}