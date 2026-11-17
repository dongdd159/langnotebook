using Microsoft.EntityFrameworkCore;

namespace LangNotebook.Data;

public class DatabaseInitializer(IDbContextFactory<ApplicationContext> factory, BrowserDbStorage storage)
{
    private Task? _initTask;
    public bool IsReady { get; private set; }
    public Task EnsureReadyAsync() => _initTask ??= InitializeAsync();
    
    private async Task InitializeAsync()
    {
        await storage.RestoreAsync();
        var changed = false;
        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = DELETE;");
            changed = await AddMissingColumnsAsync(db);
            if (!await db.Notebooks.AnyAsync())
            {
                await SeedData.ApplyAsync(db);
                changed = true;
            }
        }
        if (changed)
            await storage.PersistAsync();
        IsReady = true;
    }

    // EnsureCreated leaves existing tables alone, so databases from earlier versions need new columns added here.
    private static async Task<bool> AddMissingColumnsAsync(ApplicationContext db)
    {
        var changed = false;
        changed |= await AddColumnAsync(db, "WritingPieces", nameof(Models.WritingPiece.AiReviewJson),
            "TEXT NOT NULL DEFAULT ''");
        foreach (var table in new[] { "VocabEntries", "GrammarPatterns" })
        {
            changed |= await AddColumnAsync(db, table, nameof(Models.StudyItemBase.Stability), "REAL NOT NULL DEFAULT 0");
            changed |= await AddColumnAsync(db, table, nameof(Models.StudyItemBase.Difficulty), "REAL NOT NULL DEFAULT 0");
        }
        changed |= await AddColumnAsync(db, "Settings", nameof(Models.AppSettings.Algorithm),
            "INTEGER NOT NULL DEFAULT 0");
        changed |= await AddColumnAsync(db, "Settings", nameof(Models.AppSettings.DesiredRetention),
            "REAL NOT NULL DEFAULT 0.9");
        return changed;
    }

    // Table, column and definition are constants from AddMissingColumnsAsync, never user input.
    private static async Task<bool> AddColumnAsync(ApplicationContext db, string table, string column, string definition)
    {
        var existing = await db.Database
            .SqlQueryRaw<string>($"SELECT name AS Value FROM pragma_table_info('{table}')")
            .ToListAsync();
        if (existing.Contains(column))
            return false;
        await db.Database.ExecuteSqlRawAsync($"ALTER TABLE {table} ADD COLUMN {column} {definition};");
        return true;
    }

    public void Reset()
    {
        _initTask = null;
        IsReady = false;
    }

}