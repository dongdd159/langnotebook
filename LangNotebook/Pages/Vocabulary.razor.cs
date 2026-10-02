using LangNotebook.Dto;
using Microsoft.AspNetCore.Components;

namespace LangNotebook.Pages;

public partial class Vocabulary : ComponentBase
{
    private static readonly Models.ProficiencyLevel[] Levels =
        (Models.ProficiencyLevel[])Enum.GetValues(typeof(Models.ProficiencyLevel));
    private readonly ItemFilter _filter = new();
    private List<Models.VocabEntry> _items = new();
    private List<Models.Notebook> _notebooks = new();
    private List<string> _tags = new();
    private bool _loading = true;
    private bool _editorOpen;
    private Models.VocabEntry _draft = new();
    private string _readingLabel = "Phiên âm";
    private bool _importOpen;
    private string _importText = string.Empty;
    private string _importTags = string.Empty;
    private int _importNotebookId;
    private int _dueInList => _items.Count(x => !x.IsSuspended && x.DueUtc <= DateTime.UtcNow);
    private bool CanSave => !string.IsNullOrWhiteSpace(_draft.Term)
                            && !string.IsNullOrWhiteSpace(_draft.Meaning)
                            && _draft.NotebookId > 0;

    protected override void OnInitialized() => State.Changed += OnStateChanged;

    protected override async Task OnInitializedAsync()
    {
        _notebooks = await Library.GetNotebooksAsync();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
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

    private void OpenEditor(Models.VocabEntry? entry)
    {
        if (_notebooks.Count == 0)
        {
            State.ShowError("Hãy tạo ít nhất một sổ trước khi thêm từ.");
            return;
        }
        if (entry is null)
            _draft = new Models.VocabEntry
            {
                NotebookId = State.ActiveNotebookId ?? _notebooks[0].Id
            };
        else
            _draft = Clone(entry);
        _readingLabel = _notebooks.FirstOrDefault(x => x.Id == _draft.NotebookId)?.ReadingLabel ?? "Phiên âm";
        _editorOpen = true;
    }

    private void OnDraftNotebookChanged(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var id))
        {
            _draft.NotebookId = id;
            _readingLabel = _notebooks.FirstOrDefault(x => x.Id == id)?.ReadingLabel ?? "Phiên âm";
        }
    }

    private void OnDraftLevelChanged(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var value))
            _draft.Level = (Models.ProficiencyLevel)value;
    }

    private void OnDraftSuspendedChanged(ChangeEventArgs args) => _draft.IsSuspended = args.Value is bool b && b;

    private async Task SaveDraftAsync()
    {
        if (!CanSave)
            return;
        await Library.SaveVocabAsync(_draft);
        _editorOpen = false;
        State.ShowSuccess("Đã lưu từ vựng.");
        await LoadAsync();
        State.NotifyChanged();
    }

    private async Task ToggleFavoriteAsync(Models.VocabEntry entry)
    {
        var copy = Clone(entry);
        copy.IsFavorite = !copy.IsFavorite;
        await Library.SaveVocabAsync(copy);
        await LoadAsync();
    }

    private async Task ResetAsync(Models.VocabEntry entry)
    {
        await Reviews.ResetProgressAsync(Models.StudyItemKind.Vocabulary, entry.Id);
        State.ShowSuccess($"Đã đặt lại tiến độ của “{entry.Term}”.");
        await LoadAsync();
    }

    private async Task DeleteAsync(Models.VocabEntry entry)
    {
        var confirmed = await JS.InvokeAsync<bool>("confirm", [$"Xoá từ “{entry.Term}”?"]);
        if (!confirmed)
            return;
        await Library.DeleteVocabAsync(entry.Id);
        State.ShowSuccess("Đã xoá.");
        await LoadAsync();
        State.NotifyChanged();
    }

    private void OpenImport()
    {
        if (_notebooks.Count == 0)
        {
            State.ShowError("Hãy tạo ít nhất một sổ trước.");
            return;
        }
        _importNotebookId = State.ActiveNotebookId ?? _notebooks[0].Id;
        _importText = string.Empty;
        _importTags = string.Empty;
        _importOpen = true;
    }

    private void OnImportNotebookChanged(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var id))
            _importNotebookId = id;
    }

    private async Task RunImportAsync()
    {
        var count = await Library.BulkImportVocabAsync(_importNotebookId, _importText, _importTags);
        _importOpen = false;
        if (count == 0)
            State.ShowError("Không đọc được dòng nào. Kiểm tra lại định dạng.");
        else
            State.ShowSuccess($"Đã nhập {count} từ.");
        await LoadAsync();
        State.NotifyChanged();
    }

    private static Models.VocabEntry Clone(Models.VocabEntry source) => new()
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

    private void OnStateChanged() => InvokeAsync(async () =>
    {
        await LoadAsync();
        StateHasChanged();
    });

    public void Dispose() => State.Changed -= OnStateChanged;
}