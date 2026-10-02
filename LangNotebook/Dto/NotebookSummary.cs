namespace LangNotebook.Dto;

public sealed class NotebookSummary
{
    public int NotebookId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ColorHex { get; init; } = "#4f7df0";
    public int VocabCount { get; init; }
    public int GrammarCount { get; init; }
    public int MatureCount { get; init; }
    public int DueCount { get; init; }
}