namespace LangNotebook.Dto;

public sealed class WritingFeedback
{
    public string Kind { get; init; } = "info"; // good | warn | info
    public string Message { get; init; } = string.Empty;
}