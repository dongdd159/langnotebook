using LangNotebook.Models;

namespace LangNotebook.Data;

public class SeedData
{
    public static async Task ApplyAsync(ApplicationContext db)
    {
        var now = DateTime.UtcNow;

        var english = new Notebook
        {
            Name = "Tiếng Anh – Giao tiếp công sở",
            LanguageCode = "en",
            LanguageName = "Tiếng Anh",
            Description = "Từ vựng và mẫu câu dùng trong email, họp hành, báo cáo.",
            ColorHex = "#4f7df0",
            ReadingLabel = "IPA",
            CreatedUtc = now
        };

        var japanese = new Notebook
        {
            Name = "Tiếng Nhật – N4",
            LanguageCode = "ja",
            LanguageName = "Tiếng Nhật",
            Description = "Từ vựng, kanji và ngữ pháp trình độ N4.",
            ColorHex = "#e0663f",
            ReadingLabel = "Furigana",
            CreatedUtc = now
        };

        db.Notebooks.AddRange(english, japanese);
        await db.SaveChangesAsync();

        db.VocabEntries.AddRange(
            NewVocab(english.Id, "deadline", "/ˈdedlaɪn/", "hạn chót", "n.",
                "We need to move the deadline to next Friday.",
                "Chúng ta cần dời hạn chót sang thứ Sáu tuần sau.",
                "meet a deadline\nmiss a deadline\ntight deadline",
                "công sở,email", ProficiencyLevel.Elementary),
            NewVocab(english.Id, "follow up", "/ˈfɒləʊ ʌp/", "theo dõi tiếp, hỏi lại sau", "phr. v.",
                "I'll follow up with the client tomorrow.",
                "Tôi sẽ liên hệ lại với khách hàng vào ngày mai.",
                "follow up on an email\nfollow up with someone",
                "công sở,email", ProficiencyLevel.Intermediate),
            NewVocab(english.Id, "concise", "/kənˈsaɪs/", "ngắn gọn, súc tích", "adj.",
                "Please keep the summary concise.",
                "Hãy giữ cho phần tóm tắt thật ngắn gọn.",
                "a concise summary\nclear and concise",
                "viết,học thuật", ProficiencyLevel.UpperIntermediate),
            NewVocab(english.Id, "on behalf of", "/ɒn bɪˈhɑːf əv/", "thay mặt cho", "phrase",
                "I'm writing on behalf of the marketing team.",
                "Tôi viết thư này thay mặt cho nhóm marketing.",
                "on behalf of the company",
                "email", ProficiencyLevel.Intermediate),
            NewVocab(japanese.Id, "約束", "やくそく", "lời hứa, cuộc hẹn", "danh từ",
                "友だちと約束があります。",
                "Tôi có hẹn với bạn.",
                "約束をする\n約束を守る",
                "N4,giao tiếp", ProficiencyLevel.Elementary),
            NewVocab(japanese.Id, "準備", "じゅんび", "sự chuẩn bị", "danh từ",
                "試験の準備をしています。",
                "Tôi đang chuẩn bị cho kỳ thi.",
                "準備する\n準備ができる",
                "N4", ProficiencyLevel.Elementary)
        );

        db.GrammarPatterns.AddRange(
            new GrammarPattern
            {
                NotebookId = english.Id,
                Pattern = "S + would rather + V(bare) + than + V(bare)",
                Meaning = "Thà làm A còn hơn làm B",
                Explanation =
                    "Dùng để nêu lựa chọn ưu tiên. Sau 'would rather' và sau 'than' đều dùng động từ nguyên mẫu không 'to'. Dạng rút gọn: 'd rather.",
                Examples =
                    "I would rather stay home than go out tonight. = Tối nay tôi thà ở nhà còn hơn ra ngoài.\nShe'd rather write an email than make a call. = Cô ấy thà viết email còn hơn gọi điện.",
                CommonMistakes = "Sai: I would rather to stay. → Đúng: I would rather stay.",
                Topic = "So sánh & lựa chọn",
                Level = ProficiencyLevel.Intermediate,
                Tags = "công sở,nói",
                CreatedUtc = now,
                UpdatedUtc = now,
                DueUtc = now
            },
            new GrammarPattern
            {
                NotebookId = english.Id,
                Pattern = "It is + adj + that + S + (should) + V(bare)",
                Meaning = "Cấu trúc giả định – nhấn mạnh sự cần thiết",
                Explanation =
                    "Dùng với các tính từ như important, essential, necessary, vital. Động từ trong mệnh đề that ở dạng nguyên mẫu, kể cả với chủ ngữ số ít.",
                Examples =
                    "It is essential that every member submit the report on time. = Điều thiết yếu là mọi thành viên nộp báo cáo đúng hạn.\nIt is important that he be present at the meeting. = Điều quan trọng là anh ấy có mặt ở cuộc họp.",
                CommonMistakes = "Sai: It is essential that he submits… → Đúng: … that he submit…",
                Topic = "Câu giả định",
                Level = ProficiencyLevel.Advanced,
                Tags = "viết,học thuật",
                CreatedUtc = now,
                UpdatedUtc = now,
                DueUtc = now
            },
            new GrammarPattern
            {
                NotebookId = japanese.Id,
                Pattern = "V(て形) + もいいですか",
                Meaning = "Xin phép: “Tôi ... có được không?”",
                Explanation =
                    "Dùng để xin phép làm việc gì. Trả lời đồng ý: はい、どうぞ。Trả lời từ chối lịch sự: すみません、ちょっと…。",
                Examples = "ここに座ってもいいですか。 = Tôi ngồi ở đây có được không?\n写真を撮ってもいいですか。 = Tôi chụp ảnh có được không?",
                CommonMistakes = "Nhầm thể て: 行くて → đúng là 行って.",
                Topic = "Xin phép",
                Level = ProficiencyLevel.Elementary,
                Tags = "N4,giao tiếp",
                CreatedUtc = now,
                UpdatedUtc = now,
                DueUtc = now
            }
        );

        db.WritingPrompts.AddRange(
            new WritingPrompt
            {
                NotebookId = english.Id,
                Title = "Email dời hạn nộp báo cáo",
                PromptText =
                    "Viết một email khoảng 120–150 từ gửi quản lý, xin dời hạn nộp báo cáo thêm ba ngày. Nêu lý do, đề xuất hạn mới và cam kết chất lượng.",
                Topic = "Email công việc",
                Level = ProficiencyLevel.Intermediate,
                TargetWordCount = 140,
                SuggestedTerms = "deadline,follow up,on behalf of,concise",
                CreatedUtc = now
            },
            new WritingPrompt
            {
                NotebookId = english.Id,
                Title = "Ý kiến: làm việc từ xa",
                PromptText =
                    "Một số người cho rằng làm việc từ xa khiến năng suất giảm. Bạn đồng ý hay không? Viết 200–250 từ, nêu ít nhất hai lý do và một ví dụ cụ thể.",
                Topic = "Nghị luận",
                Level = ProficiencyLevel.UpperIntermediate,
                TargetWordCount = 220,
                SuggestedTerms = "productivity,flexible,commute,collaboration",
                CreatedUtc = now
            },
            new WritingPrompt
            {
                NotebookId = japanese.Id,
                Title = "自己紹介",
                PromptText =
                    "Viết đoạn giới thiệu bản thân khoảng 150 chữ: tên, công việc, sở thích, lý do học tiếng Nhật và mục tiêu trong một năm tới.",
                Topic = "Giới thiệu",
                Level = ProficiencyLevel.Elementary,
                TargetWordCount = 150,
                SuggestedTerms = "約束,準備",
                CreatedUtc = now
            }
        );

        db.Settings.Add(new AppSettings
        {
            Id = 1,
            DailyReviewGoal = 30,
            NewCardsPerSession = 10,
            MaxCardsPerSession = 40,
            ActiveNotebookId = english.Id,
            WeeklyWritingGoal = 600
        });

        await db.SaveChangesAsync();
    }

    private static VocabEntry NewVocab(int notebookId, string term, string reading, string meaning, string partOfSpeech,
        string example, string exampleMeaning, string collocations, string tags, ProficiencyLevel level)
    {
        var now = DateTime.UtcNow;
        return new VocabEntry
        {
            NotebookId = notebookId, Term = term, Reading = reading, Meaning = meaning, PartOfSpeech = partOfSpeech,
            Example = example, ExampleMeaning = exampleMeaning, Collocations = collocations, Tags = tags, Level = level,
            CreatedUtc = now, UpdatedUtc = now, DueUtc = now
        };
    }
}