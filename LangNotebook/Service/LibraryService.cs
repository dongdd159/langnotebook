using LangNotebook.Data;
using LangNotebook.Dto;
using LangNotebook.Models;
using Microsoft.EntityFrameworkCore;

namespace LangNotebook.Service;

public sealed class LibraryService(
    IDbContextFactory<ApplicationContext> factory,
    BrowserDbStorage storage,
    DatabaseInitializer initializer)
{
    private async Task<ApplicationContext> CreateAsync()
    {
        await initializer.EnsureReadyAsync();
        return await factory.CreateDbContextAsync();
    }

    public async Task<List<Notebook>> GetNotebooksAsync(bool includeArchived = false)
    {
        await using var db = await CreateAsync();
        var query = db.Notebooks.AsNoTracking().AsQueryable();
        if (!includeArchived)
            query = query.Where(x => !x.IsArchived);
        return await query.OrderBy(x => x.Name).ToListAsync();
    }

    public async Task<Notebook?> GetNotebookAsync(int id)
    {
        await using var db = await CreateAsync();
        return await db.Notebooks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<int> SaveNotebookAsync(Notebook notebook)
    {
        await using (var db = await CreateAsync())
        {
            if (notebook.Id == 0)
            {
                notebook.CreatedUtc = DateTime.UtcNow;
                db.Notebooks.Add(notebook);
            }
            else
                db.Notebooks.Update(notebook);
            await db.SaveChangesAsync();
        }
        await storage.PersistAsync();
        return notebook.Id;
    }

    public async Task DeleteNotebookAsync(int id)
    {
        await using var db = await CreateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            await db.VocabEntries.Where(x => x.NotebookId == id).ExecuteDeleteAsync();
            await db.GrammarPatterns.Where(x => x.NotebookId == id).ExecuteDeleteAsync();
            await db.WritingPrompts.Where(x => x.NotebookId == id).ExecuteDeleteAsync();
            await db.WritingPieces.Where(x => x.NotebookId == id).ExecuteDeleteAsync();
            await db.ReviewLogs.Where(x => x.NotebookId == id).ExecuteDeleteAsync();
            await db.Notebooks.Where(x => x.Id == id).ExecuteDeleteAsync();
            await transaction.CommitAsync();
            await storage.PersistAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<VocabEntry>> GetVocabAsync(ItemFilter filter)
    {
        await using var db = await CreateAsync();
        var query = db.VocabEntries.AsNoTracking().AsQueryable();
        if (filter.NotebookId.HasValue)
            query = query.Where(x => x.NotebookId == filter.NotebookId.Value);
        var items = await query.ToListAsync();
        return ApplyCommonFilters(items, filter, x => $"{x.Term} {x.Reading} {x.Meaning} {x.Example} {x.Note}")
            .ToList();
    }

    public async Task<VocabEntry?> GetVocabByIdAsync(int id)
    {
        await using var db = await CreateAsync();
        return await db.VocabEntries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<int> SaveVocabAsync(VocabEntry entry)
    {
        await using (var db = await CreateAsync())
        {
            entry.UpdatedUtc = DateTime.UtcNow;

            if (entry.Id == 0)
            {
                entry.CreatedUtc = DateTime.UtcNow;
                db.VocabEntries.Add(entry);
            }
            else
                db.VocabEntries.Update(entry);
            await db.SaveChangesAsync();
        }
        await storage.PersistAsync();
        return entry.Id;
    }

    public async Task DeleteVocabAsync(int id)
    {
        await using (var db = await CreateAsync())
        {
            var entry = await db.VocabEntries.FirstOrDefaultAsync(x => x.Id == id);
            if (entry is not null)
            {
                db.VocabEntries.Remove(entry);
                await db.SaveChangesAsync();
            }
        }
        await storage.PersistAsync();
    }
    
    public async Task<int> BulkImportVocabAsync(int notebookId, string rawText, string defaultTags = "")
    {
        var lines = rawText.Split(new[] { "\r\n", "\r", "\n" },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var added = new List<VocabEntry>();
        var now = DateTime.UtcNow;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#"))
                continue;
            var parts = line.Split('|').Select(x => x.Trim()).ToArray();
            if (parts.Length == 0 || parts[0].Length == 0)
                continue;
            var entry = new VocabEntry
            {
                NotebookId = notebookId,
                Term = parts[0],
                CreatedUtc = now,
                UpdatedUtc = now,
                DueUtc = now,
                Tags = defaultTags
            };
            if (parts.Length == 2)
                entry.Meaning = parts[1];
            else if (parts.Length >= 3)
            {
                entry.Reading = parts[1];
                entry.Meaning = parts[2];
                if (parts.Length > 3) entry.PartOfSpeech = parts[3];
                if (parts.Length > 4) entry.Example = parts[4];
                if (parts.Length > 5) entry.ExampleMeaning = parts[5];
                if (parts.Length > 6 && parts[6].Length > 0)
                    entry.Tags = string.IsNullOrWhiteSpace(defaultTags) ? parts[6] : $"{defaultTags},{parts[6]}";
            }
            added.Add(entry);
        }
        if (added.Count == 0)
            return 0;
        await using (var db = await CreateAsync())
        {
            db.VocabEntries.AddRange(added);
            await db.SaveChangesAsync();
        }
        await storage.PersistAsync();
        return added.Count;
    }
    
    public async Task<List<GrammarPattern>> GetGrammarAsync(ItemFilter filter)
    {
        await using var db = await CreateAsync();
        var query = db.GrammarPatterns.AsNoTracking().AsQueryable();
        if (filter.NotebookId.HasValue)
            query = query.Where(x => x.NotebookId == filter.NotebookId.Value);
        var items = await query.ToListAsync();
        return ApplyCommonFilters(items, filter, x => $"{x.Pattern} {x.Meaning} {x.Explanation} {x.Examples} {x.Topic}")
            .ToList();
    }

    public async Task<GrammarPattern?> GetGrammarByIdAsync(int id)
    {
        await using var db = await CreateAsync();
        return await db.GrammarPatterns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<int> SaveGrammarAsync(GrammarPattern pattern)
    {
        await using (var db = await CreateAsync())
        {
            pattern.UpdatedUtc = DateTime.UtcNow;

            if (pattern.Id == 0)
            {
                pattern.CreatedUtc = DateTime.UtcNow;
                db.GrammarPatterns.Add(pattern);
            }
            else
                db.GrammarPatterns.Update(pattern);
            await db.SaveChangesAsync();
        }
        await storage.PersistAsync();
        return pattern.Id;
    }

    public async Task DeleteGrammarAsync(int id)
    {
        await using (var db = await CreateAsync())
        {
            var pattern = await db.GrammarPatterns.FirstOrDefaultAsync(x => x.Id == id);
            if (pattern is not null)
            {
                db.GrammarPatterns.Remove(pattern);
                await db.SaveChangesAsync();
            }
        }
        await storage.PersistAsync();
    }
    
    public async Task<List<string>> GetTagsAsync(int? notebookId)
    {
        await using var db = await CreateAsync();
        var vocabTags = await db.VocabEntries.AsNoTracking()
            .Where(x => !notebookId.HasValue || x.NotebookId == notebookId.Value).Select(x => x.Tags).ToListAsync();
        var grammarTags = await db.GrammarPatterns.AsNoTracking()
            .Where(x => !notebookId.HasValue || x.NotebookId == notebookId.Value).Select(x => x.Tags).ToListAsync();
        return vocabTags.Concat(grammarTags)
            .SelectMany(x => x.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<VocabEntry>> GetSuggestedVocabAsync(int notebookId, int take = 12)
    {
        await using var db = await CreateAsync();
        return await db.VocabEntries.AsNoTracking().Where(x => x.NotebookId == notebookId && !x.IsSuspended)
            .OrderByDescending(x => x.UpdatedUtc).Take(take).ToListAsync();
    }

    private static IEnumerable<T> ApplyCommonFilters<T>(List<T> items, ItemFilter filter,
        Func<T, string> searchTextSelector) where T : StudyItemBase
    {
        IEnumerable<T> result = items;
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var needle = filter.Search.Trim();
            result = result.Where(x => searchTextSelector(x).Contains(needle, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(filter.Tag))
            result = result.Where(x =>
                x.TagList.Any(t => string.Equals(t, filter.Tag, StringComparison.OrdinalIgnoreCase)));
        if (filter.OnlyFavorite)
            result = result.Where(x => x.IsFavorite);
        if (filter.OnlyDue)
        {
            var now = DateTime.UtcNow;
            result = result.Where(x => !x.IsSuspended && x.DueUtc <= now);
        }
        if (filter.Stage.HasValue)
            result = result.Where(x => x.Stage == filter.Stage.Value);
        result = filter.SortBy switch
        {
            "alpha" => result.OrderBy(x => searchTextSelector(x), StringComparer.OrdinalIgnoreCase),
            "due" => result.OrderBy(x => x.DueUtc),
            "hardest" => result.OrderByDescending(x => x.Lapses).ThenBy(x => x.EaseFactor),
            "oldest" => result.OrderBy(x => x.CreatedUtc),
            _ => result.OrderByDescending(x => x.UpdatedUtc)
        };
        return result;
    }
}