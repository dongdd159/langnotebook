using System.Text;
using System.Text.Json;
using LangNotebook.Data;
using LangNotebook.Dto;
using LangNotebook.Helper;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace LangNotebook.Service;

public sealed class BackupService(
    IDbContextFactory<ApplicationContext> factory,
    BrowserDbStorage storage,
    DatabaseInitializer initializer,
    IJSRuntime js)
{
    public async Task<string> ExportJsonAsync()
    {
        await initializer.EnsureReadyAsync();
        await using var db = await factory.CreateDbContextAsync();
        var payload = new BackupPayload
        {
            ExportedUtc = DateTime.UtcNow,
            Notebooks = await db.Notebooks.AsNoTracking().ToListAsync(),
            VocabEntries = await db.VocabEntries.AsNoTracking().ToListAsync(),
            GrammarPatterns = await db.GrammarPatterns.AsNoTracking().ToListAsync(),
            WritingPrompts = await db.WritingPrompts.AsNoTracking().ToListAsync(),
            WritingPieces = await db.WritingPieces.AsNoTracking().ToListAsync(),
            ReviewLogs = await db.ReviewLogs.AsNoTracking().ToListAsync(),
            Settings = await db.Settings.AsNoTracking().FirstOrDefaultAsync()
        };
        return JsonSerializer.Serialize(payload, BackupJsonContext.Default.BackupPayload);
    }

    public async Task DownloadJsonAsync()
    {
        var json = await ExportJsonAsync();
        var fileName = $"book-{DateTime.Now:yyyy-MM-dd-HHmm}.json";
        await js.InvokeVoidAsync("langDb.download", fileName, "application/json", Encoding.UTF8.GetBytes(json));
    }

    private const string BackupFormat = "lang-notebook-backup";
    private const int CurrentVersion = 1;
    private const string DefaultColor = "#4f7df0";

    /// <summary>
    /// Reads a backup file and checks it before anything is changed. Rows that can't be used (no word, no notebook
    /// name) are dropped, and notebook colours are limited to #rrggbb because they end up in style attributes.
    /// </summary>
    public static BackupPayload ParseBackup(string json)
    {
        BackupPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize(json, BackupJsonContext.Default.BackupPayload);
        }
        catch (JsonException)
        {
            payload = null;
        }
        if (payload is null || payload.Format != BackupFormat)
            throw new InvalidOperationException("The file isn't a Language Notebook backup.");
        if (payload.Version > CurrentVersion)
            throw new InvalidOperationException(
                "This backup was made by a newer version of the app. Update the app and try again.");

        payload.Notebooks = (payload.Notebooks ?? []).Where(x => !string.IsNullOrWhiteSpace(x.Name)).ToList();
        foreach (var notebook in payload.Notebooks)
            if (!IsHexColor(notebook.ColorHex))
                notebook.ColorHex = DefaultColor;
        payload.VocabEntries = (payload.VocabEntries ?? []).Where(x => !string.IsNullOrWhiteSpace(x.Term)).ToList();
        payload.GrammarPatterns =
            (payload.GrammarPatterns ?? []).Where(x => !string.IsNullOrWhiteSpace(x.Pattern)).ToList();
        payload.WritingPrompts = payload.WritingPrompts ?? [];
        payload.WritingPieces = payload.WritingPieces ?? [];
        payload.ReviewLogs = payload.ReviewLogs ?? [];
        if (payload.Settings is not null && payload.Settings.DesiredRetention is < 0.7 or > 0.97)
            payload.Settings.DesiredRetention = 0.9;
        return payload;
    }

    private static bool IsHexColor(string? value) =>
        value is { Length: 7 } && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);

    /// <summary>
    /// Loads a backup. The file is checked first, and the whole import (including clearing old data when
    /// <paramref name="replaceExisting"/> is set) runs in one transaction, so a bad file never leaves the database half
    /// replaced. Review history is re-pointed at the new item IDs.
    /// </summary>
    public async Task<int> ImportJsonAsync(string json, bool replaceExisting)
    {
        var payload = ParseBackup(json);
        await initializer.EnsureReadyAsync();
        var imported = 0;
        await using (var db = await factory.CreateDbContextAsync())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            if (replaceExisting)
            {
                await db.ReviewLogs.ExecuteDeleteAsync();
                await db.WritingPieces.ExecuteDeleteAsync();
                await db.WritingPrompts.ExecuteDeleteAsync();
                await db.VocabEntries.ExecuteDeleteAsync();
                await db.GrammarPatterns.ExecuteDeleteAsync();
                await db.Notebooks.ExecuteDeleteAsync();
            }

            var oldNotebookIds = payload.Notebooks.Select(x => x.Id).ToList();
            foreach (var notebook in payload.Notebooks)
                notebook.Id = 0;
            db.Notebooks.AddRange(payload.Notebooks);
            await db.SaveChangesAsync();
            var notebookMap = MapIds(oldNotebookIds, payload.Notebooks.Select(x => x.Id));
            imported += payload.Notebooks.Count;

            var vocab = payload.VocabEntries.Where(x => notebookMap.ContainsKey(x.NotebookId)).ToList();
            var oldVocabIds = vocab.Select(x => x.Id).ToList();
            foreach (var entry in vocab)
            {
                entry.Id = 0;
                entry.NotebookId = notebookMap[entry.NotebookId];
            }
            db.VocabEntries.AddRange(vocab);

            var grammar = payload.GrammarPatterns.Where(x => notebookMap.ContainsKey(x.NotebookId)).ToList();
            var oldGrammarIds = grammar.Select(x => x.Id).ToList();
            foreach (var pattern in grammar)
            {
                pattern.Id = 0;
                pattern.NotebookId = notebookMap[pattern.NotebookId];
            }
            db.GrammarPatterns.AddRange(grammar);

            var prompts = payload.WritingPrompts.Where(x => notebookMap.ContainsKey(x.NotebookId)).ToList();
            var oldPromptIds = prompts.Select(x => x.Id).ToList();
            foreach (var prompt in prompts)
            {
                prompt.Id = 0;
                prompt.NotebookId = notebookMap[prompt.NotebookId];
            }
            db.WritingPrompts.AddRange(prompts);
            await db.SaveChangesAsync();
            var vocabMap = MapIds(oldVocabIds, vocab.Select(x => x.Id));
            var grammarMap = MapIds(oldGrammarIds, grammar.Select(x => x.Id));
            var promptMap = MapIds(oldPromptIds, prompts.Select(x => x.Id));
            imported += vocab.Count + grammar.Count + prompts.Count;

            var pieces = payload.WritingPieces.Where(x => notebookMap.ContainsKey(x.NotebookId)).ToList();
            foreach (var piece in pieces)
            {
                piece.Id = 0;
                piece.NotebookId = notebookMap[piece.NotebookId];
                piece.PromptId = piece.PromptId is { } promptId && promptMap.TryGetValue(promptId, out var newPrompt)
                    ? newPrompt
                    : null;
            }
            db.WritingPieces.AddRange(pieces);

            var logs = new List<Models.ReviewLog>();
            foreach (var log in payload.ReviewLogs)
            {
                var items = log.ItemKind == Models.StudyItemKind.Vocabulary ? vocabMap : grammarMap;
                if (!notebookMap.TryGetValue(log.NotebookId, out var notebookId) ||
                    !items.TryGetValue(log.ItemId, out var itemId))
                    continue;
                log.Id = 0;
                log.NotebookId = notebookId;
                log.ItemId = itemId;
                logs.Add(log);
            }
            db.ReviewLogs.AddRange(logs);
            imported += pieces.Count + logs.Count;

            if (payload.Settings is not null && replaceExisting)
            {
                var settings = await db.Settings.FirstOrDefaultAsync();
                if (settings is not null)
                {
                    settings.DailyReviewGoal = payload.Settings.DailyReviewGoal;
                    settings.NewCardsPerSession = payload.Settings.NewCardsPerSession;
                    settings.MaxCardsPerSession = payload.Settings.MaxCardsPerSession;
                    settings.WeeklyWritingGoal = payload.Settings.WeeklyWritingGoal;
                    settings.DisplayName = payload.Settings.DisplayName;
                    settings.DesiredRetention = payload.Settings.DesiredRetention;
                    settings.ActiveNotebookId = null;
                }
            }
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        await storage.PersistAsync();
        return imported;
    }

    private static Dictionary<int, int> MapIds(IEnumerable<int> oldIds, IEnumerable<int> newIds)
    {
        var map = new Dictionary<int, int>();
        foreach (var (oldId, newId) in oldIds.Zip(newIds))
            map[oldId] = newId;
        return map;
    }

    public async Task DownloadDatabaseAsync()
    {
        await initializer.EnsureReadyAsync();
        var bytes = await storage.ReadRawAsync();
        if (bytes.Length == 0)
            return;
        var fileName = $"notebook-{DateTime.Now:yyyy-MM-dd-HHmm}.db";
        await js.InvokeVoidAsync("langDb.download", fileName, "application/octet-stream", bytes);
    }

    public async Task RestoreDatabaseAsync(byte[] bytes)
    {
        // Every SQLite database starts with this text. Anything else would replace the data with a file the app
        // can't open.
        if (bytes.Length < 100 || Encoding.ASCII.GetString(bytes, 0, 15) != "SQLite format 3")
            throw new InvalidOperationException("The file isn't a SQLite database.");
        await storage.WriteRawAsync(bytes);
        initializer.Reset();
        await initializer.EnsureReadyAsync();
    }

    public async Task WipeAsync()
    {
        await storage.ClearAsync();
        initializer.Reset();
        await initializer.EnsureReadyAsync();
    }
}