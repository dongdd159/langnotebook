using LangNotebook.Data;
using LangNotebook.Models;
using Microsoft.EntityFrameworkCore;

namespace LangNotebook.Service;

public class SettingsService(
    IDbContextFactory<ApplicationContext> factory,
    BrowserDbStorage storage,
    DatabaseInitializer initializer)
{
    private readonly IDbContextFactory<ApplicationContext> _factory = factory;
    private readonly BrowserDbStorage _storage = storage;
    private readonly DatabaseInitializer _initializer = initializer;
    
    public async Task<AppSettings> GetAsync()
    {
        await _initializer.EnsureReadyAsync();
        await using var db = await _factory.CreateDbContextAsync();
        var settings = await db.Settings.AsNoTracking().FirstOrDefaultAsync();
        return settings ?? new AppSettings();
    }
    
    public async Task SaveAsync(AppSettings settings)
    {
        await _initializer.EnsureReadyAsync();
        await using (var db = await _factory.CreateDbContextAsync())
        {
            var existing = await db.Settings.FirstOrDefaultAsync(x => x.Id == settings.Id);
            if (existing is null)
                db.Settings.Add(settings);
            else
            {
                existing.DailyReviewGoal = settings.DailyReviewGoal;
                existing.NewCardsPerSession = settings.NewCardsPerSession;
                existing.MaxCardsPerSession = settings.MaxCardsPerSession;
                existing.DefaultDirection = settings.DefaultDirection;
                existing.ActiveNotebookId = settings.ActiveNotebookId;
                existing.WeeklyWritingGoal = settings.WeeklyWritingGoal;
                existing.DisplayName = settings.DisplayName;
            }
            await db.SaveChangesAsync();
        }
        await _storage.PersistAsync();
    }
}