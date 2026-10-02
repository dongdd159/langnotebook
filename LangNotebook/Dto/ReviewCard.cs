using LangNotebook.Models;

namespace LangNotebook.Dto;

public sealed class ReviewCard
{
    public StudyItemKind Kind { get; init; }
    public int Id { get; init; }
    public int NotebookId { get; init; }
    public string NotebookName { get; set; } = string.Empty;
    public string Front { get; init; } = string.Empty;
    public string Reading { get; init; } = string.Empty;
    public string Back { get; init; } = string.Empty;
    public string Extra { get; init; } = string.Empty;
    public string Example { get; init; } = string.Empty;
    public string ExampleMeaning { get; init; } = string.Empty;
    public string Tags { get; init; } = string.Empty;
    public required StudyItemBase Item { get; init; }
    public bool IsNew => Item.Repetitions == 0;
}
