using LangNotebook.Pages.Vocabulary;
using Microsoft.AspNetCore.Components;

namespace LangNotebook.Pages.Vocabulary;

public partial class BulkImport : ComponentBase
{
    [CascadingParameter] public VocabularyCascadingContext Context { get; set; } = default!;
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public EventCallback<int> OnRunImport { get; set; }
    private int _notebookId;
    private string _importText = string.Empty;
    private string _importTags = string.Empty;

    protected override void OnParametersSet()
    {
        _notebookId = State.ActiveNotebookId ?? Context.Notebooks[0].Id;
        _importText = string.Empty;
        _importTags = string.Empty;
    }
    
    private void OnNotebookChanged(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var id))
            _notebookId = id;
    }
    
    private async Task RunImportAsync()
    {
        var count = await Library.BulkImportVocabAsync(_notebookId, _importText, _importTags);
        IsOpen = false;
        await IsOpenChanged.InvokeAsync(IsOpen);
        await OnRunImport.InvokeAsync(count);
    }
    
    private async Task CancelImportAsync()
    {
        IsOpen = false;
        await IsOpenChanged.InvokeAsync(IsOpen);
    }

}