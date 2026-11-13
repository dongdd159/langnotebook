namespace LangNotebook.Dto;

/// <summary>One parsed line of the vocabulary bulk-import text.</summary>
public sealed class BulkImportRow
{
    public int LineNumber { get; init; }
    public string Term { get; init; } = string.Empty;
    public string Reading { get; init; } = string.Empty;
    public string Meaning { get; init; } = string.Empty;
    public string PartOfSpeech { get; init; } = string.Empty;
    public string Example { get; init; } = string.Empty;
    public string ExampleMeaning { get; init; } = string.Empty;
    public string Tags { get; init; } = string.Empty;

    /// <summary>Why the line will not be imported; null when it is ready.</summary>
    public string? SkipReason { get; init; }

    public bool IsReady => SkipReason is null;
}
