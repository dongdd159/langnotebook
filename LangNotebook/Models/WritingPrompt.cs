using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LangNotebook.Models;

public class WritingPrompt
{
    public int Id { get; set; }
    public int NotebookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string PromptText { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public ProficiencyLevel Level { get; set; } = ProficiencyLevel.Intermediate;
    public int TargetWordCount { get; set; } = 150;
    public string SuggestedTerms { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    [NotMapped]
    [JsonIgnore]
    public string[] SuggestedTermList =>
        SuggestedTerms.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
