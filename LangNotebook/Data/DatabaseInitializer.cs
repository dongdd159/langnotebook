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
        var seeded = false;
        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
            if (!await db.Notebooks.AnyAsync())
            {
                await SeedData.ApplyAsync(db);
                seeded = true;
            }
        }
        if (seeded)
            await storage.PersistAsync();
        IsReady = true;
    }
    
    public void Reset()
    {
        _initTask = null;
        IsReady = false;
    }

}