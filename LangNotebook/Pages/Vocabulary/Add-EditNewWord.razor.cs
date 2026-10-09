using LangNotebook.Helper;
using LangNotebook.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LangNotebook.Pages.Vocabulary;

public partial class Add_EditNewWord : ComponentBase
{
    [CascadingParameter] public VocabularyCascadingContext Context { get; set; } = default!;
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public required VocabEntry Entry { get; set; }
    [Parameter] public EventCallback<VocabEntry> EntryChanged { get; set; }
    [Parameter] public EventCallback<VocabEntry> OnSaved { get; set; }
    private string _readingLabel = "Reading label";
    private bool CanSave => !string.IsNullOrWhiteSpace(Entry?.Term) && !string.IsNullOrWhiteSpace(Entry.Meaning)
                            && Entry.NotebookId > 0;

    protected override void OnParametersSet() => _readingLabel =
        Context.Notebooks.FirstOrDefault(x => x.Id == Entry.NotebookId)?.ReadingLabel ?? "Reading label";
    
    private void OnDraftNotebookChanged(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var id))
        {
            Entry.NotebookId = id;
            _readingLabel = Context.Notebooks.FirstOrDefault(x => x.Id == id)?.ReadingLabel ?? "Reading label";
        }
    }

    private void OnDraftLevelChanged(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var value))
            Entry.Level = (ProficiencyLevel)value;
    }

    private void OnDraftSuspendedChanged(ChangeEventArgs args) => Entry!.IsSuspended = args.Value is bool b && b;

    private async Task SaveDraftAsync()
    {
        if (!CanSave)
            return;
        IsOpen = false;
        await IsOpenChanged.InvokeAsync(IsOpen);
        await OnSaved.InvokeAsync(Entry);
    }
    
    private async Task CancelDraftAsync()
    {
        IsOpen = false;
        await IsOpenChanged.InvokeAsync(IsOpen);
    }
}