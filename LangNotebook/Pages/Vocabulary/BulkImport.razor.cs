using LangNotebook.Dto;
using LangNotebook.Service;
using Microsoft.AspNetCore.Components;

namespace LangNotebook.Pages.Vocabulary;

public partial class BulkImport : ComponentBase
{
    private const int PreviewLimit = 50;

    [CascadingParameter] public VocabularyCascadingContext Context { get; set; } = default!;
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public EventCallback<int> OnRunImport { get; set; }
    private int _notebookId;
    private string _importText = string.Empty;
    private string _importTags = string.Empty;
    private bool _wasOpen;
    private bool _importing;

    private List<BulkImportRow> Rows => LibraryService.ParseBulkVocab(_importText, _importTags);
    private bool HasNotebooks => Context?.Notebooks is { Count: > 0 };

    protected override void OnParametersSet()
    {
        // Reset the form only when the dialog opens, not on every parent re-render,
        // so typing is not lost when the page refreshes underneath the dialog.
        if (IsOpen && !_wasOpen)
        {
            _notebookId = State.ActiveNotebookId ?? (HasNotebooks ? Context.Notebooks[0].Id : 0);
            _importText = string.Empty;
            _importTags = string.Empty;
            _importing = false;
        }
        _wasOpen = IsOpen;
    }

    private void OnNotebookChanged(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var id))
            _notebookId = id;
    }

    private async Task RunImportAsync()
    {
        if (_importing || _notebookId == 0)
            return;
        _importing = true;
        try
        {
            var count = await Library.BulkImportVocabAsync(_notebookId, _importText, _importTags);
            IsOpen = false;
            await IsOpenChanged.InvokeAsync(IsOpen);
            await OnRunImport.InvokeAsync(count);
        }
        finally
        {
            _importing = false;
        }
    }

    private async Task CancelImportAsync()
    {
        IsOpen = false;
        await IsOpenChanged.InvokeAsync(IsOpen);
    }

    private static string OrDash(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
}
