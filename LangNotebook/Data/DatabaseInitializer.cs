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
        var existing = await db.Database
            .SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info('WritingPieces')")
            .ToListAsync();
        if (existing.Contains(nameof(Models.WritingPiece.AiReviewJson)))
            return false;
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE WritingPieces ADD COLUMN AiReviewJson TEXT NOT NULL DEFAULT '';");
        return true;
    }

    public void Reset()
    {
        _initTask = null;
        IsReady = false;
    }

}