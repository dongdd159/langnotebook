using LangNotebook.Dto;
using LangNotebook.Models;
using Microsoft.AspNetCore.Components;

namespace LangNotebook.Pages.Notebooks;

public partial class ManageNotebooks : ComponentBase
{
    private static readonly (string Name, string Hex)[] Palette =
    [
        ("Orange", "#c2571a"), ("Blue", "#1f6b8f"), ("Purple", "#6a43a8"), ("Green", "#2f7a55"),
        ("Red", "#b3392f"), ("Ochre", "#a8650f"), ("Slate", "#4a5568"), ("Rose", "#a8325e")
    ];

    private List<Notebook> _notebooks = new();
    private Dictionary<int, NotebookSummary> _counts = new();
    private Notebook _form = NewNotebook();
    private int? _confirmDeleteId;
    private bool _loading = true;
    private bool _saving;

    private bool IsNew => _form.Id == 0;
    private int ActiveCount => _notebooks.Count(x => !x.IsArchived);
    private int ArchivedCount => _notebooks.Count(x => x.IsArchived);

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        var first = _notebooks.FirstOrDefault(x => !x.IsArchived);
        if (first is not null)
            Edit(first);
        _loading = false;
    }

    private async Task LoadAsync()
    {
        _notebooks = (await Library.GetNotebooksAsync(includeArchived: true))
            .OrderBy(x => x.IsArchived).ThenBy(x => x.Name).ToList();
        var stats = await StatsSvc.GetAsync(null);
        _counts = stats.Notebooks.ToDictionary(x => x.NotebookId);
    }

    private static Notebook NewNotebook() => new()
    {
        Name = string.Empty,
        ColorHex = Palette[3].Hex,
        ReadingLabel = "Reading"
    };

    private void StartNew()
    {
        _confirmDeleteId = null;
        _form = NewNotebook();
    }

    private void Edit(Notebook notebook)
    {
        _confirmDeleteId = null;
        _form = new Notebook
        {
            Id = notebook.Id,
            Name = notebook.Name,
            LanguageName = notebook.LanguageName,
            LanguageCode = notebook.LanguageCode,
            Description = notebook.Description,
            ColorHex = notebook.ColorHex,
            ReadingLabel = notebook.ReadingLabel,
            IsArchived = notebook.IsArchived,
            CreatedUtc = notebook.CreatedUtc
        };
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_form.Name))
        {
            State.ShowError("Give the notebook a name.");
            return;
        }
        _saving = true;
        try
        {
            _form.Name = _form.Name.Trim();
            var wasNew = IsNew;
            var id = await Library.SaveNotebookAsync(_form);
            await LoadAsync();
            var saved = _notebooks.FirstOrDefault(x => x.Id == id);
            if (saved is not null)
                Edit(saved);
            State.ShowSuccess(wasNew ? "Notebook created." : "Changes saved.");
            State.NotifyChanged();
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task ToggleArchiveAsync(Notebook notebook)
    {
        var copy = await Library.GetNotebookAsync(notebook.Id);
        if (copy is null)
            return;
        copy.IsArchived = !copy.IsArchived;
        await Library.SaveNotebookAsync(copy);
        if (copy.IsArchived)
            await ClearActiveIfAsync(copy.Id);
        await LoadAsync();
        if (_form.Id == copy.Id)
            Edit(copy);
        State.ShowSuccess(copy.IsArchived ? $"“{copy.Name}” archived." : $"“{copy.Name}” restored.");
        State.NotifyChanged();
    }

    private void AskDelete(Notebook notebook) => _confirmDeleteId = notebook.Id;

    private void CancelDelete() => _confirmDeleteId = null;

    private async Task DeleteAsync(Notebook notebook)
    {
        await Library.DeleteNotebookAsync(notebook.Id);
        await ClearActiveIfAsync(notebook.Id);
        _confirmDeleteId = null;
        await LoadAsync();
        if (_form.Id == notebook.Id)
            StartNew();
        State.ShowSuccess($"“{notebook.Name}” deleted.");
        State.NotifyChanged();
    }

    private async Task ClearActiveIfAsync(int notebookId)
    {
        if (State.ActiveNotebookId != notebookId)
            return;
        var settings = await SettingsSvc.GetAsync();
        settings.ActiveNotebookId = null;
        await SettingsSvc.SaveAsync(settings);
        State.ActiveNotebookId = null;
    }

    private void PickColor(string hex) => _form.ColorHex = hex;

    private int Count(int notebookId, Func<NotebookSummary, int> pick) =>
        _counts.TryGetValue(notebookId, out var summary) ? pick(summary) : 0;

    private static string LanguageLabel(Notebook notebook)
    {
        var parts = new[] { notebook.LanguageName, notebook.LanguageCode }
            .Where(x => !string.IsNullOrWhiteSpace(x));
        return string.Join(" · ", parts);
    }
}
