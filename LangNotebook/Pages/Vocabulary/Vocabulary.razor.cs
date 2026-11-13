using LangNotebook.Dto;
using LangNotebook.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LangNotebook.Pages.Vocabulary;

public partial class Vocabulary : ComponentBase
{
    private VocabularyCascadingContext _appContext = new();
    private static readonly ProficiencyLevel[] Levels = (ProficiencyLevel[])Enum.GetValues(typeof(ProficiencyLevel));
    private readonly ItemFilter _filter = new();
    private List<VocabEntry> _items = new();
    private List<Notebook> _notebooks = new();
    private List<string> _tags = new();
    private bool _loading = true;
    private bool _editorOpen;
    private bool _importOpen;
    private int DueInList => _items.Count(x => !x.IsSuspended && x.DueUtc <= DateTime.UtcNow);
    private static readonly int[] PageSizeOptions = { 10, 20, 50, 100 };
    private int _pageIndex = 1;
    private int _pageSize = 10;
    private string _pageKey = string.Empty;
    private ElementReference _tableTop;
    private int TotalPages => Math.Max(1, (int)Math.Ceiling(_items.Count / (double)_pageSize));
    private VocabEntry _selectedEntry = new();
    private int? _detailId;

    /// <summary>The word shown in the detail panel; null when the panel is closed or the word left the list.</summary>
    private VocabEntry? Detail => _detailId is null ? null : _items.FirstOrDefault(x => x.Id == _detailId);

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
        _items = await Library.GetVocabAsync(_filter);
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

    private async Task OnFavoriteChanged(ChangeEventArgs args)
    {
        _filter.OnlyFavorite = args.Value is bool b && b;
        await LoadAsync();
    }

    private void OpenEditor(VocabEntry? entry)
    {
        if (_notebooks.Count == 0)
        {
            State.ShowError("Please create at least one notebook before adding words.");
            return;
        }
        if (entry is null)
            _selectedEntry = new VocabEntry
            {
                NotebookId = State.ActiveNotebookId ?? _notebooks[0].Id
            };
        else
            _selectedEntry = Clone(entry);
        _editorOpen = true;
    }

    private async Task AddNewWord(VocabEntry draft)
    {
        await Library.SaveVocabAsync(draft);
        State.ShowSuccess("Saved.");
        await LoadAsync();
        State.NotifyChanged();
    }

    private async Task ToggleFavoriteAsync(VocabEntry entry)
    {
        var copy = Clone(entry);
        copy.IsFavorite = !copy.IsFavorite;
        await Library.SaveVocabAsync(copy);
        await LoadAsync();
    }

    private async Task ResetAsync(VocabEntry entry)
    {
        await Reviews.ResetProgressAsync(StudyItemKind.Vocabulary, entry.Id);
        State.ShowSuccess($"Reset progress for “{entry.Term}”.");
        await LoadAsync();
    }

    private async Task DeleteAsync(VocabEntry entry)
    {
        var confirmed = await JS.InvokeAsync<bool>("confirm", [$"Delete word “{entry.Term}”?"]);
        if (!confirmed)
            return;
        await Library.DeleteVocabAsync(entry.Id);
        State.ShowSuccess("Deleted.");
        await LoadAsync();
        State.NotifyChanged();
    }

    private static readonly (string Key, string Label)[] StatusFilters =
    [
        ("all", "All"), ("due", "Due"), ("starred", "Starred"),
        ("new", "New"), ("learning", "Learning"), ("young", "Young"), ("mature", "Mature")
    ];

    /// <summary>The status segment that matches the current filter flags.</summary>
    private string CurrentStatus =>
        _filter.OnlyDue ? "due"
        : _filter.OnlyFavorite ? "starred"
        : _filter.Stage switch
        {
            LearningStage.New => "new",
            LearningStage.Learning => "learning",
            LearningStage.Young => "young",
            LearningStage.Mature => "mature",
            _ => "all"
        };

    private async Task SetStatusAsync(string key)
    {
        _filter.OnlyDue = key == "due";
        _filter.OnlyFavorite = key == "starred";
        _filter.Stage = key switch
        {
            "new" => LearningStage.New,
            "learning" => LearningStage.Learning,
            "young" => LearningStage.Young,
            "mature" => LearningStage.Mature,
            _ => null
        };
        await LoadAsync();
    }

    private void ShowDetail(VocabEntry entry) => _detailId = _detailId == entry.Id ? null : entry.Id;

    private void CloseDetail() => _detailId = null;

    private async Task ToggleSuspendAsync(VocabEntry entry)
    {
        var copy = Clone(entry);
        copy.IsSuspended = !copy.IsSuspended;
        await Library.SaveVocabAsync(copy);
        State.ShowSuccess(copy.IsSuspended
            ? $"“{entry.Term}” won’t appear in reviews."
            : $"“{entry.Term}” is back in reviews.");
        await LoadAsync();
        State.NotifyChanged();
    }

    private void OpenImport()
    {
        if (_notebooks.Count == 0)
        {
            State.ShowError("Please create at least one notebook first.");
            return;
        }
        _importOpen = true;
    }

    private async Task AfterImportAsync(int count)
    {
        if (count == 0)
            State.ShowError("Failed to parse any lines. Please check the format.");
        else
            State.ShowSuccess($"Imported {count} words.");
        await LoadAsync();
        State.NotifyChanged();
    }

    private static VocabEntry Clone(VocabEntry source) => new()
    {
        Id = source.Id,
        NotebookId = source.NotebookId,
        Term = source.Term,
        Reading = source.Reading,
        Meaning = source.Meaning,
        PartOfSpeech = source.PartOfSpeech,
        Example = source.Example,
        ExampleMeaning = source.ExampleMeaning,
        Collocations = source.Collocations,
        Synonyms = source.Synonyms,
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
    
    private void SyncPage()
    {
        var key = string.Join('|', _filter.Search, _filter.Tag, _filter.SortBy, _filter.OnlyDue, _filter.OnlyFavorite, _filter.Stage, State.ActiveNotebookId);
        if (key != _pageKey)
        {
            _pageKey = key;
            _pageIndex = 1;
        }
        _pageIndex = Math.Clamp(_pageIndex, 1, TotalPages);
    }

    private async Task GoToPageAsync(int page)
    {
        _pageIndex = Math.Clamp(page, 1, TotalPages);
        try
        {
            await _tableTop.FocusAsync();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void OnPageSizeChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var size) && size > 0)
        {
            _pageSize = size;
            _pageIndex = 1;
        }
    }

    private List<int> VisiblePages()
    {
        var total = TotalPages;
        var pages = new SortedSet<int> { 1, total, _pageIndex - 1, _pageIndex, _pageIndex + 1 };
        if (_pageIndex <= 3)
        {
            pages.Add(2);
            pages.Add(3);
        }
        if (_pageIndex >= total - 2)
        {
            pages.Add(total - 1);
            pages.Add(total - 2);
        }
        var result = new List<int>();
        var previous = 0;
        foreach (var page in pages.Where(p => p >= 1 && p <= total))
        {
            if (previous != 0)
                if (page - previous == 2)
                    result.Add(previous + 1);
                else if (page - previous > 2)
                    result.Add(0);
            result.Add(page);
            previous = page;
        }
        return result;
    }

    private void OnStateChanged() => InvokeAsync(async () =>
    {
        await LoadAsync();
        StateHasChanged();
    });

    public void Dispose() => State.Changed -= OnStateChanged;
}