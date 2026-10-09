using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LangNotebook.Models;

public class VocabEntry : StudyItemBase
{
    public string Term { get; set; } = string.Empty;
    public string Reading { get; set; } = string.Empty;
    public string Meaning { get; set; } = string.Empty;
    public string PartOfSpeech { get; set; } = string.Empty;
    public string Example { get; set; } = string.Empty;
    public string ExampleMeaning { get; set; } = string.Empty;
    public string Collocations { get; set; } = string.Empty;
    public string Synonyms { get; set; } = string.Empty;
    public ProficiencyLevel Level { get; set; } = ProficiencyLevel.Elementary;
    [NotMapped]
    [JsonIgnore]
    public string DisplayTitle => string.IsNullOrWhiteSpace(Term) ? "(untitled)" : Term;
}
