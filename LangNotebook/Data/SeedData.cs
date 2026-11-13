using LangNotebook.Models;

namespace LangNotebook.Data;

public class SeedData
{
    public static async Task ApplyAsync(ApplicationContext db)
    {
        var now = DateTime.UtcNow;
        var english = new Notebook
        {
            Name = "English – Business Communication",
            LanguageCode = "en",
            LanguageName = "English",
            Description = "Vocabulary and phrases used in emails, meetings, and reports.",
            ColorHex = "#4f7df0",
            ReadingLabel = "IPA",
            CreatedUtc = now
        };
        db.Notebooks.Add(english);
        await db.SaveChangesAsync();
        db.VocabEntries.AddRange(
            NewVocab(english.Id, "deadline", "/ˈdedlaɪn/", "a time limit for finishing something", "n.",
                "We need to move the deadline to next Friday.",
                "The deadline is being moved to next Friday.",
                "meet a deadline\nmiss a deadline\ntight deadline",
                "work,email", ProficiencyLevel.Elementary),
            NewVocab(english.Id, "follow up", "/ˈfɒləʊ ʌp/", "to contact someone again about something", "phr. v.",
                "I'll follow up with the client tomorrow.",
                "I will contact the client again tomorrow.",
                "follow up on an email\nfollow up with someone",
                "work,email", ProficiencyLevel.Intermediate),
            NewVocab(english.Id, "concise", "/kənˈsaɪs/", "short and clear", "adj.",
                "Please keep the summary concise.",
                "Keep the summary short and clear.",
                "a concise summary\nclear and concise",
                "writing,academic", ProficiencyLevel.UpperIntermediate),
            NewVocab(english.Id, "on behalf of", "/ɒn bɪˈhɑːf əv/", "representing someone", "phrase",
                "I'm writing on behalf of the marketing team.",
                "I am writing as a representative of the marketing team.",
                "on behalf of the company",
                "email", ProficiencyLevel.Intermediate)
        );
        db.GrammarPatterns.AddRange(
            new GrammarPattern
            {
                NotebookId = english.Id,
                Pattern = "S + would rather + V(bare) + than + V(bare)",
                Meaning = "Prefer doing A to doing B",
                Explanation =
                    "Used to state a preference. Both after 'would rather' and after 'than', use the bare infinitive (no 'to'). Short form: 'd rather.",
                Examples =
                    "I would rather stay home than go out tonight. = I prefer staying home to going out tonight.\nShe'd rather write an email than make a call. = She prefers emailing to calling.",
                CommonMistakes = "Wrong: I would rather to stay. → Right: I would rather stay.",
                Topic = "Preferences & choices",
                Level = ProficiencyLevel.Intermediate,
                Tags = "work,speaking",
                CreatedUtc = now,
                UpdatedUtc = now,
                DueUtc = now
            },
            new GrammarPattern
            {
                NotebookId = english.Id,
                Pattern = "It is + adj + that + S + (should) + V(bare)",
                Meaning = "Subjunctive – stresses that something is necessary",
                Explanation =
                    "Used with adjectives such as important, essential, necessary and vital. The verb in the that-clause stays in the base form, even with a singular subject.",
                Examples =
                    "It is essential that every member submit the report on time. = Every member must hand in the report on time.\nIt is important that he be present at the meeting. = He needs to attend the meeting.",
                CommonMistakes = "Wrong: It is essential that he submits… → Right: … that he submit…",
                Topic = "Subjunctive",
                Level = ProficiencyLevel.Advanced,
                Tags = "writing,academic",
                CreatedUtc = now,
                UpdatedUtc = now,
                DueUtc = now
            },
            new GrammarPattern
            {
                NotebookId = english.Id,
                Pattern = "V(て形) + もいいですか",
                Meaning = "Asking permission: “May I …?”",
                Explanation =
                    "Used to ask for permission. To say yes: はい、どうぞ。To decline politely: すみません、ちょっと…。",
                Examples = "ここに座ってもいいですか。 = May I sit here?\n写真を撮ってもいいですか。 = May I take a photo?",
                CommonMistakes = "Wrong て-form: 行くて → the correct form is 行って.",
                Topic = "Permission",
                Level = ProficiencyLevel.Elementary,
                Tags = "N4,conversation",
                CreatedUtc = now,
                UpdatedUtc = now,
                DueUtc = now
            }
        );
        db.WritingPrompts.AddRange(
            new WritingPrompt
            {
                NotebookId = english.Id,
                Title = "Email: asking to move a report deadline",
                PromptText =
                    "Write a 120–150 word email to your manager asking to move a report deadline by three days. Give a reason, suggest a new date and promise the quality will not suffer.",
                Topic = "Work email",
                Level = ProficiencyLevel.Intermediate,
                TargetWordCount = 140,
                SuggestedTerms = "deadline,follow up,on behalf of,concise",
                CreatedUtc = now
            },
            new WritingPrompt
            {
                NotebookId = english.Id,
                Title = "Opinion: remote work",
                PromptText =
                    "Some people think remote work lowers productivity. Do you agree or disagree? Write 200–250 words with at least two reasons and one concrete example.",
                Topic = "Argument",
                Level = ProficiencyLevel.UpperIntermediate,
                TargetWordCount = 220,
                SuggestedTerms = "productivity,flexible,commute,collaboration",
                CreatedUtc = now
            },
            new WritingPrompt
            {
                NotebookId = english.Id,
                Title = "自己紹介",
                PromptText =
                    "Write a self-introduction of about 150 characters: your name, job, hobbies, why you study Japanese and your goal for the next year.",
                Topic = "Introductions",
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