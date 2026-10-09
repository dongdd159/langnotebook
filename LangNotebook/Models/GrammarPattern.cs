using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LangNotebook.Models;

public class GrammarPattern : StudyItemBase
{
    public string Pattern { get; set; } = string.Empty;
    public string Meaning { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string Examples { get; set; } = string.Empty;
    public string CommonMistakes { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public ProficiencyLevel Level { get; set; } = ProficiencyLevel.Intermediate;

    [NotMapped]
    [JsonIgnore]
    public string DisplayTitle => string.IsNullOrWhiteSpace(Pattern) ? "(untitled)" : Pattern;

    [NotMapped]
    [JsonIgnore]
    public IEnumerable<string> ExampleLines => Examples.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Trim()).Where(x => x.Length > 0);
}