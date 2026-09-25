using LangNotebook.Models;

namespace LangNotebook.Helper;

public static class Labels
{
    public static string Stage(LearningStage stage) => stage switch
    {
        LearningStage.New => "Mới",
        LearningStage.Learning => "Đang học",
        LearningStage.Young => "Chưa vững",
        _ => "Đã thuộc"
    };

    public static string Level(ProficiencyLevel level) => level switch
    {
        ProficiencyLevel.Beginner => "Mới bắt đầu",
        ProficiencyLevel.Elementary => "Sơ cấp",
        ProficiencyLevel.Intermediate => "Trung cấp",
        ProficiencyLevel.UpperIntermediate => "Trung cao cấp",
        _ => "Cao cấp"
    };

    public static string Scope(ReviewScope scope) => scope switch
    {
        ReviewScope.All => "Từ vựng + cấu trúc",
        ReviewScope.VocabularyOnly => "Chỉ từ vựng",
        _ => "Chỉ cấu trúc"
    };

    public static string Kind(StudyItemKind kind) => kind == StudyItemKind.Vocabulary ? "Từ vựng" : "Cấu trúc";

    public static string Status(WritingStatus status) => status == WritingStatus.Draft ? "Bản nháp" : "Đã xong";

    public static string Due(StudyItemBase item)
    {
        if (item.IsSuspended)
            return "tạm ngưng";
        if (item.Repetitions == 0)
            return "chưa học";
        var now = DateTime.UtcNow;
        if (item.DueUtc <= now)
            return "đến hạn";
        var days = (int)Math.Ceiling((item.DueUtc - now).TotalDays);
        return days <= 1 ? "trong hôm nay" : $"còn {days} ngày";
    }

    public static string RelativeDate(DateTime utc)
    {
        var local = utc.ToLocalTime();
        var days = (int)(DateTime.Now.Date - local.Date).TotalDays;
        return days switch
        {
            0 => $"hôm nay {local:HH:mm}",
            1 => $"hôm qua {local:HH:mm}",
            < 7 => $"{days} ngày trước",
            _ => local.ToString("dd/MM/yyyy")
        };
    }

    public static string Duration(int seconds)
    {
        if (seconds <= 0)
            return "0 phút";
        var span = TimeSpan.FromSeconds(seconds);
        if (span.TotalMinutes < 1)
            return $"{seconds} giây";
        if (span.TotalHours < 1)
            return $"{(int)span.TotalMinutes} phút";
        return $"{(int)span.TotalHours} giờ {span.Minutes} phút";
    }
}