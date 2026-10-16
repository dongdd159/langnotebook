using LangNotebook.Data;
using LangNotebook.Dto;
using LangNotebook.Models;
using Microsoft.EntityFrameworkCore;

namespace LangNotebook.Service;

public class ReviewService(
    IDbContextFactory<ApplicationContext> _factory,
    BrowserDbStorage _storage,
    DatabaseInitializer _initializer,
    LibraryService _library)
{
    
    private async Task<ApplicationContext> CreateAsync()
    {
        await _initializer.EnsureReadyAsync();
        return await _factory.CreateDbContextAsync();
    }

    public async Task<(int Due, int New)> CountQueueAsync(int? notebookId)
    {
        await using var db = await CreateAsync();
        var now = DateTime.UtcNow;
        var vocabDue = await db.VocabEntries.AsNoTracking().CountAsync(x =>
            !x.IsSuspended && x.Repetitions > 0 && x.DueUtc <= now &&
            (!notebookId.HasValue || x.NotebookId == notebookId.Value));
        var grammarDue = await db.GrammarPatterns.AsNoTracking().CountAsync(x =>
            !x.IsSuspended && x.Repetitions > 0 && x.DueUtc <= now &&
            (!notebookId.HasValue || x.NotebookId == notebookId.Value));
        var vocabNew = await db.VocabEntries.AsNoTracking().CountAsync(x =>
            !x.IsSuspended && x.Repetitions == 0 && (!notebookId.HasValue || x.NotebookId == notebookId.Value));
        var grammarNew = await db.GrammarPatterns.AsNoTracking().CountAsync(x =>
            !x.IsSuspended && x.Repetitions == 0 && (!notebookId.HasValue || x.NotebookId == notebookId.Value));
        return (vocabDue + grammarDue, vocabNew + grammarNew);
    }

    public async Task<List<ReviewCard>> BuildSessionAsync(ReviewScope scope, ItemFilter itemFilter,
        AppSettings settings, bool includeNew = true)
    {
        await using var db = await CreateAsync();
        var now = DateTime.UtcNow;
        var due = new List<ReviewCard>();
        var fresh = new List<ReviewCard>();
        var notebooks = await db.Notebooks.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name);
        if (scope != ReviewScope.GrammarOnly)
        {
            var vocab = await db.VocabEntries.AsNoTracking()
                .Where(x => !x.IsSuspended &&
                            (!itemFilter.NotebookId.HasValue || x.NotebookId == itemFilter.NotebookId.Value))
                .ToListAsync();
            vocab = LibraryService.ApplyCommonFilters(vocab, itemFilter, _ => string.Empty).ToList();
            foreach (var entry in vocab)
            {
                var card = ToCard(entry);
                card.NotebookName = notebooks.TryGetValue(entry.NotebookId, out var n) ? n : string.Empty;
                if (entry.Repetitions == 0)
                    fresh.Add(card);
                else if (entry.DueUtc <= now)
                    due.Add(card);
            }
        }
        if (scope != ReviewScope.VocabularyOnly)
        {
            var grammar = await db.GrammarPatterns.AsNoTracking().Where(x =>
                    !x.IsSuspended && (!itemFilter.NotebookId.HasValue || x.NotebookId == itemFilter.NotebookId.Value))
                .ToListAsync();
            grammar = LibraryService.ApplyCommonFilters(grammar, itemFilter, _ => string.Empty).ToList();
            foreach (var pattern in grammar)
            {
                var card = ToCard(pattern);
                card.NotebookName = notebooks.TryGetValue(pattern.NotebookId, out var n) ? n : string.Empty;
                if (pattern.Repetitions == 0)
                    fresh.Add(card);
                else if (pattern.DueUtc <= now)
                    due.Add(card);
            }
        }
        var random = new Random();
        var queue = due.OrderBy(_ => random.Next()).ToList();
        if (includeNew && settings.NewCardsPerSession > 0)
            queue.AddRange(fresh.OrderBy(x => x.Item.CreatedUtc).Take(settings.NewCardsPerSession));
        if (settings.MaxCardsPerSession > 0 && queue.Count > settings.MaxCardsPerSession)
            queue = queue.Take(settings.MaxCardsPerSession).ToList();
        return queue.OrderBy(_ => random.Next()).ToList();
    }

    public async Task<SrsOutcome> GradeAsync(ReviewCard card, ReviewGrade grade, int durationMs)
    {
        SrsOutcome outcome;
        var now = DateTime.UtcNow;
        await using (var db = await CreateAsync())
        {
            StudyItemBase? tracked = card.Kind == StudyItemKind.Vocabulary
                ? await db.VocabEntries.FirstOrDefaultAsync(x => x.Id == card.Id)
                : await db.GrammarPatterns.FirstOrDefaultAsync(x => x.Id == card.Id);
            if (tracked is null)
                return new SrsOutcome(card.Item.EaseFactor, card.Item.IntervalDays, card.Item.Repetitions,
                    card.Item.Lapses, card.Item.DueUtc);
            outcome = SrsScheduler.Apply(tracked, grade, now);
            db.ReviewLogs.Add(new ReviewLog
            {
                NotebookId = tracked.NotebookId, ItemKind = card.Kind, ItemId = tracked.Id, Grade = grade,
                ReviewedUtc = now, IntervalAfterDays = outcome.IntervalDays, EaseAfter = outcome.EaseFactor,
                DurationMs = durationMs
            });
            await db.SaveChangesAsync();
            card.Item.EaseFactor = outcome.EaseFactor;
            card.Item.IntervalDays = outcome.IntervalDays;
            card.Item.Repetitions = outcome.Repetitions;
            card.Item.Lapses = outcome.Lapses;
            card.Item.DueUtc = outcome.DueUtc;
            card.Item.LastReviewedUtc = now;
        }
        await _storage.PersistAsync();
        return outcome;
    }

    public async Task ResetProgressAsync(StudyItemKind kind, int id)
    {
        await using (var db = await CreateAsync())
        {
            StudyItemBase? tracked = kind == StudyItemKind.Vocabulary
                ? await db.VocabEntries.FirstOrDefaultAsync(x => x.Id == id)
                : await db.GrammarPatterns.FirstOrDefaultAsync(x => x.Id == id);
            if (tracked is null)
                return;
            tracked.EaseFactor = 2.5;
            tracked.IntervalDays = 0;
            tracked.Repetitions = 0;
            tracked.Lapses = 0;
            tracked.LastReviewedUtc = null;
            tracked.DueUtc = DateTime.UtcNow;
            tracked.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        await _storage.PersistAsync();
    }

    private static ReviewCard ToCard(VocabEntry entry) => new()
    {
        Kind = StudyItemKind.Vocabulary,
        Id = entry.Id,
        NotebookId = entry.NotebookId,
        Front = entry.Term,
        Reading = entry.Reading,
        Back = entry.Meaning,
        Extra = entry.PartOfSpeech,
        Example = entry.Example,
        ExampleMeaning = entry.ExampleMeaning,
        Tags = entry.Tags,
        Item = entry
    };
    
    private static ReviewCard ToCard(GrammarPattern pattern) => new()
    {
        Kind = StudyItemKind.Grammar,
        Id = pattern.Id,
        NotebookId = pattern.NotebookId,
        Front = pattern.Pattern,
        Reading = string.Empty,
        Back = pattern.Meaning,
        Extra = pattern.Explanation,
        Example = pattern.Examples,
        ExampleMeaning = string.Empty,
        Tags = pattern.Tags,
        Item = pattern
    };
}