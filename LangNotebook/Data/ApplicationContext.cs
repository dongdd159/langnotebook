using LangNotebook.Models;
using Microsoft.EntityFrameworkCore;

namespace LangNotebook.Data;

public class ApplicationContext(DbContextOptions<ApplicationContext> options) : DbContext(options)
{
    public DbSet<Notebook> Notebooks => Set<Notebook>();
    public DbSet<VocabEntry> VocabEntries => Set<VocabEntry>();
    public DbSet<GrammarPattern> GrammarPatterns => Set<GrammarPattern>();
    public DbSet<WritingPrompt> WritingPrompts => Set<WritingPrompt>();
    public DbSet<WritingPiece> WritingPieces => Set<WritingPiece>();
    public DbSet<ReviewLog> ReviewLogs => Set<ReviewLog>();
    public DbSet<AppSettings> Settings => Set<AppSettings>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Notebook>(e =>
        {
            e.ToTable("Notebooks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired();
            e.HasIndex(x => x.Name);
        });
        modelBuilder.Entity<VocabEntry>(e =>
        {
            e.ToTable("VocabEntries");
            e.HasKey(x => x.Id);
            e.Property(x => x.Term).IsRequired();
            e.HasIndex(x => x.NotebookId);
            e.HasIndex(x => x.DueUtc);
            e.HasIndex(x => x.Term);
        });
        modelBuilder.Entity<GrammarPattern>(e =>
        {
            e.ToTable("GrammarPatterns");
            e.HasKey(x => x.Id);
            e.Property(x => x.Pattern).IsRequired();
            e.HasIndex(x => x.NotebookId);
            e.HasIndex(x => x.DueUtc);
        });
        modelBuilder.Entity<WritingPrompt>(e =>
        {
            e.ToTable("WritingPrompts");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.NotebookId);
        });
        modelBuilder.Entity<WritingPiece>(e =>
        {
            e.ToTable("WritingPieces");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.NotebookId);
            e.HasIndex(x => x.UpdatedUtc);
        });
        modelBuilder.Entity<ReviewLog>(e =>
        {
            e.ToTable("ReviewLogs");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ReviewedUtc);
            e.HasIndex(x => x.NotebookId);
        });
        modelBuilder.Entity<AppSettings>(e =>
        {
            e.ToTable("Settings");
            e.HasKey(x => x.Id);
        });
    }
}