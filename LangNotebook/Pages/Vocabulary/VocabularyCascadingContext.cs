using LangNotebook.Helper;
using LangNotebook.Models;
using LangNotebook.Service;

namespace LangNotebook.Pages.Vocabulary;

public class VocabularyCascadingContext
{
    public List<Notebook> Notebooks { get; set; }
    public ProficiencyLevel[] Levels { get; set; }
}   