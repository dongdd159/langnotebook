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

    public async Task<int> ImportJsonAsync(string json, bool replaceExisting)
    {
        var payload = JsonSerializer.Deserialize(json, BackupJsonContext.Default.BackupPayload) ??
                      throw new InvalidOperationException("Backupfile is not valid.");
        await initializer.EnsureReadyAsync();
        var imported = 0;
        await using (var db = await factory.CreateDbContextAsync())
        {
            if (replaceExisting)
            {
                await db.ReviewLogs.ExecuteDeleteAsync();
                await db.WritingPieces.ExecuteDeleteAsync();
                await db.WritingPrompts.ExecuteDeleteAsync();
                await db.VocabEntries.ExecuteDeleteAsync();
                await db.GrammarPatterns.ExecuteDeleteAsync();
                await db.Notebooks.ExecuteDeleteAsync();
            }
            var notebookMap = new Dictionary<int, int>();
            foreach (var notebook in payload.Notebooks)
            {
                var oldId = notebook.Id;
                notebook.Id = 0;
                db.Notebooks.Add(notebook);
                await db.SaveChangesAsync();
                notebookMap[oldId] = notebook.Id;
                imported++;
            }
            foreach (var entry in payload.VocabEntries)
            {
                entry.Id = 0;
                entry.NotebookId = notebookMap.TryGetValue(entry.NotebookId, out var newId) ? newId : entry.NotebookId;
                db.VocabEntries.Add(entry);
                imported++;
            }
            foreach (var pattern in payload.GrammarPatterns)
            {
                pattern.Id = 0;
                pattern.NotebookId = notebookMap.TryGetValue(pattern.NotebookId, out var newId)
                    ? newId
                    : pattern.NotebookId;
                db.GrammarPatterns.Add(pattern);
                imported++;
            }
            foreach (var prompt in payload.WritingPrompts)
            {
                prompt.Id = 0;
                prompt.NotebookId = notebookMap.TryGetValue(prompt.NotebookId, out var newId)
                    ? newId
                    : prompt.NotebookId;
                db.WritingPrompts.Add(prompt);
                imported++;
            }
            foreach (var piece in payload.WritingPieces)
            {
                piece.Id = 0;
                piece.PromptId = null;
                piece.NotebookId = notebookMap.TryGetValue(piece.NotebookId, out var newId) ? newId : piece.NotebookId;
                db.WritingPieces.Add(piece);
                imported++;
            }
            foreach (var log in payload.ReviewLogs)
            {
                log.Id = 0;
                log.NotebookId = notebookMap.TryGetValue(log.NotebookId, out var newId) ? newId : log.NotebookId;
                db.ReviewLogs.Add(log);
                imported++;
            }
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
                    settings.ActiveNotebookId = null;
                }
            }
            await db.SaveChangesAsync();
        }
        await storage.PersistAsync();
        return imported;
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