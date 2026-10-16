using LangNotebook.Dto;
using LangNotebook.Models;
using LangNotebook.Pages.Vocabulary;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyModel;

namespace LangNotebook.Pages.Grammar;

public partial class Grammar : ComponentBase
{
    private VocabularyCascadingContext _appContext = new();
    private static readonly ProficiencyLevel[] Levels = (ProficiencyLevel[])Enum.GetValues(typeof(ProficiencyLevel));
    private readonly ItemFilter _filter = new();
    private List<GrammarPattern> _items = new();
    private List<Notebook> _notebooks = new();
    private List<string> _tags = new();
    private bool _loading = true;
    private bool _editorOpen;
    private GrammarPattern _draft = new();

    protected override void OnInitialized() => State.Changed += OnStateChanged;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        _notebooks = await Library.GetNotebooksAsync();
        _appContext = new()
        {
            Levels = Levels,
            Notebooks = _notebooks
        };
        _filter.NotebookId = State.ActiveNotebookId;
        _items = await Library.GetGrammarAsync(_filter);
        _tags = await Library.GetTagsAsync(State.ActiveNotebookId);
        _loading = false;
    }

    private async Task OnSearchInput(ChangeEventArgs args)
    {
        _filter.Search = args.Value?.ToString() ?? string.Empty;
        await LoadAsync();
    }

    private async Task OnTagChanged(ChangeEventArgs args)
    {
        _filter.Tag = args.Value?.ToString() ?? string.Empty;
        await LoadAsync();
    }

    private async Task OnSortChanged(ChangeEventArgs args)
    {
        _filter.SortBy = args.Value?.ToString() ?? "recent";
        await LoadAsync();
    }

    private async Task OnDueChanged(ChangeEventArgs args)
    {
        _filter.OnlyDue = args.Value is bool b && b;
        await LoadAsync();
    }

    private void OpenEditor(GrammarPattern? item)
    {
        if (_notebooks.Count == 0)
        {
            State.ShowError("Create at least one notebook first.");
            return;
        }
        _draft = item is null
            ? new GrammarPattern { NotebookId = State.ActiveNotebookId ?? _notebooks[0].Id } : Clone(item);
        _editorOpen = true;
    }
    
    private async Task AddNewPattern(GrammarPattern draft)
    {
        await Library.SaveGrammarAsync(draft);
        State.ShowSuccess("Saved.");
        await LoadAsync();
        State.NotifyChanged();
    }

    private async Task ResetAsync(GrammarPattern item)
    {
        await Reviews.ResetProgressAsync(StudyItemKind.Grammar, item.Id);
        State.ShowSuccess("The schedule has been rescheduled.");
        await LoadAsync();
    }

    private async Task DeleteAsync(Models.GrammarPattern item)
    {
        var confirmed = await JS.InvokeAsync<bool>("confirm", args: ["Delete structure “{item.Pattern}”?"]);
        if (!confirmed)
            return;
        await Library.DeleteGrammarAsync(item.Id);
        State.ShowSuccess("Deleted.");
        await LoadAsync();
        State.NotifyChanged();
    }

    private static GrammarPattern Clone(GrammarPattern source) => new()
    {
        Id = source.Id,
        NotebookId = source.NotebookId,
        Pattern = source.Pattern,
        Meaning = source.Meaning,
        Explanation = source.Explanation,
        Examples = source.Examples,
        CommonMistakes = source.CommonMistakes,
        Topic = source.Topic,
        Level = source.Level,
        Tags = source.Tags,
        Note = source.Note,
        IsFavorite = source.IsFavorite,
        IsSuspended = source.IsSuspended,
        EaseFactor = source.EaseFactor,
        IntervalDays = source.IntervalDays,
        Repetitions = source.Repetitions,
        Lapses = source.Lapses,
        LastReviewedUtc = source.LastReviewedUtc,
        DueUtc = source.DueUtc,
        CreatedUtc = source.CreatedUtc,
        UpdatedUtc = source.UpdatedUtc
    };

    private void OnStateChanged()
    {
        InvokeAsync(async () =>
        {
            await LoadAsync();
            StateHasChanged();
        });
    }

    public void Dispose() => State.Changed -= OnStateChanged;
}