using LangNotebook.Models;

namespace LangNotebook.Dto;

public class ItemFilter
{
    public int? NotebookId { get; set; }
    public string Search { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public bool OnlyDue { get; set; }
    public bool OnlyFavorite { get; set; }
    public LearningStage? Stage { get; set; }
    public string SortBy { get; set; } = "recent";
}