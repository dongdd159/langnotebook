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
        changed |= await AddColumnAsync(db, "Settings", nameof(Models.AppSettings.DesiredRetention),
            "REAL NOT NULL DEFAULT 0.9");
        changed |= await RemoveSm2ColumnsAsync(db);
        return changed;
    }

    // Databases from before FSRS also carry the SM-2 columns. Cards that were scheduled by SM-2 and have no FSRS state
    // get an estimate first (interval as stability, ease factor mapped to difficulty), then the columns are dropped.
    private static async Task<bool> RemoveSm2ColumnsAsync(ApplicationContext db)
    {
        var changed = false;
        foreach (var table in new[] { "VocabEntries", "GrammarPatterns" })
        {
            if (!await HasColumnAsync(db, table, "EaseFactor"))
                continue;
            await db.Database.ExecuteSqlRawAsync(
                $"UPDATE {table} SET Stability = MAX(IntervalDays, 0.4), " +
                "Difficulty = MIN(10, MAX(1, 5.3 + (2.5 - EaseFactor) * 3.5)) WHERE Stability <= 0 AND Repetitions > 0;");
            await db.Database.ExecuteSqlRawAsync($"ALTER TABLE {table} DROP COLUMN EaseFactor;");
            changed = true;
        }
        changed |= await DropColumnAsync(db, "ReviewLogs", "EaseAfter");
        changed |= await DropColumnAsync(db, "Settings", "Algorithm");
        return changed;
    }

    private static async Task<bool> HasColumnAsync(ApplicationContext db, string table, string column) =>
        (await db.Database.SqlQueryRaw<string>($"SELECT name AS Value FROM pragma_table_info('{table}')").ToListAsync())
        .Contains(column);

    private static async Task<bool> DropColumnAsync(ApplicationContext db, string table, string column)
    {
        if (!await HasColumnAsync(db, table, column))
            return false;
        await db.Database.ExecuteSqlRawAsync($"ALTER TABLE {table} DROP COLUMN {column};");
        return true;
    }

    // Table, column and definition are constants from AddMissingColumnsAsync, never user input.
    private static async Task<bool> AddColumnAsync(ApplicationContext db, string table, string column, string definition)
    {
        if (await HasColumnAsync(db, table, column))
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