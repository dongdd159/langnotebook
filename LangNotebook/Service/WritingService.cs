using LangNotebook.Data;
using LangNotebook.Models;
using Microsoft.EntityFrameworkCore;

namespace LangNotebook.Service;

public sealed class WritingService(
    IDbContextFactory<ApplicationContext> factory,
    BrowserDbStorage storage,
    DatabaseInitializer initializer)
{
    private readonly IDbContextFactory<ApplicationContext> _factory = factory;
    private readonly BrowserDbStorage _storage = storage;
    private readonly DatabaseInitializer _initializer = initializer;
    
    private async Task<ApplicationContext> CreateAsync()
    {
        await _initializer.EnsureReadyAsync();
        return await _factory.CreateDbContextAsync();
    }
    
    public async Task<List<WritingPrompt>> GetPromptsAsync(int? notebookId)
    {
        await using var db = await CreateAsync();
        return await db.WritingPrompts.AsNoTracking()
            .Where(x => !notebookId.HasValue || x.NotebookId == notebookId.Value).OrderByDescending(x => x.CreatedUtc)
            .ToListAsync();
    }
    
    public async Task<WritingPrompt?> GetPromptAsync(int id)
    {
        await using var db = await CreateAsync();
        return await db.WritingPrompts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    }
    
    public async Task<int> SavePromptAsync(WritingPrompt prompt)
    {
        await using (var db = await CreateAsync())
        {
            if (prompt.Id == 0)
            {
                prompt.CreatedUtc = DateTime.UtcNow;
                db.WritingPrompts.Add(prompt);
            }
            else
                db.WritingPrompts.Update(prompt);
            await db.SaveChangesAsync();
        }
        await _storage.PersistAsync();
        return prompt.Id;
    }
    
    public async Task DeletePromptAsync(int id)
    {
        await using (var db = await CreateAsync())
        {
            var prompt = await db.WritingPrompts.FirstOrDefaultAsync(x => x.Id == id);
            if (prompt is not null)
            {
                db.WritingPrompts.Remove(prompt);
                await db.SaveChangesAsync();
            }
        }
        await _storage.PersistAsync();
    }
    
    public async Task<List<WritingPiece>> GetPiecesAsync(int? notebookId, string search = "")
    {
        await using var db = await CreateAsync();
        var pieces = await db.WritingPieces.AsNoTracking()
            .Where(x => !notebookId.HasValue || x.NotebookId == notebookId.Value).OrderByDescending(x => x.UpdatedUtc)
            .ToListAsync();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            pieces = pieces.Where(x => $"{x.Title} {x.Content}".Contains(needle, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        return pieces;
    }
    
    public async Task<WritingPiece?> GetPieceAsync(int id)
    {
        await using var db = await CreateAsync();
        return await db.WritingPieces.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    }
    
    public async Task<int> SavePieceAsync(WritingPiece piece)
    {
        piece.WordCount = WritingAnalyzer.CountWords(piece.Content);
        piece.UpdatedUtc = DateTime.UtcNow;
        await using (var db = await CreateAsync())
        {
            if (piece.Id == 0)
            {
                piece.CreatedUtc = DateTime.UtcNow;
                db.WritingPieces.Add(piece);
            }
            else
                db.WritingPieces.Update(piece);
            await db.SaveChangesAsync();
        }
        await _storage.PersistAsync();
        return piece.Id;
    }
    
    public async Task DeletePieceAsync(int id)
    {
        await using (var db = await CreateAsync())
        {
            var piece = await db.WritingPieces.FirstOrDefaultAsync(x => x.Id == id);
            if (piece is not null)
            {
                db.WritingPieces.Remove(piece);
                await db.SaveChangesAsync();
            }
        }
        await _storage.PersistAsync();
    }
}