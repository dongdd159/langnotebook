using LangNotebook.Models;

namespace LangNotebook.Dto;

public sealed class BackupPayload
{
    public string Format { get; set; } = "lang-notebook-backup";
    public int Version { get; set; } = 1;
    public DateTime ExportedUtc { get; set; } = DateTime.UtcNow;
    public List<Notebook> Notebooks { get; set; } = new();
    public List<VocabEntry> VocabEntries { get; set; } = new();
    public List<GrammarPattern> GrammarPatterns { get; set; } = new();
    public List<WritingPrompt> WritingPrompts { get; set; } = new();
    public List<WritingPiece> WritingPieces { get; set; } = new();
    public List<ReviewLog> ReviewLogs { get; set; } = new();
    public AppSettings? Settings { get; set; }
}