using LangNotebook.Models;
using LangNotebook.Pages.Vocabulary;
using Microsoft.AspNetCore.Components;

namespace LangNotebook.Pages.Grammar;

public partial class Add_EditGrammar : ComponentBase
{
    [CascadingParameter] 
    public VocabularyCascadingContext Context { get; set; } = default!;
    [Parameter] 
    public bool IsOpen { get; set; }
    [Parameter] 
    public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] 
    public required GrammarPattern Entry { get; set; }
    [Parameter] 
    public EventCallback<GrammarPattern> EntryChanged { get; set; }
    [Parameter] 
    public EventCallback<GrammarPattern> OnSaved { get; set; }
    [Parameter] 
    public EventCallback<string> OnCreateTag { get; set; }
    [Parameter] 
    public List<string> Tags { get; set; }
    private bool CanSave => !string.IsNullOrWhiteSpace(Entry.Pattern) && !string.IsNullOrWhiteSpace(Entry.Meaning) &&
                            Entry.NotebookId > 0;
    
    private void OnDraftNotebookChanged(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var id))
            Entry.NotebookId = id;
    }

    private void OnDraftLevelChanged(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var value))
            Entry.Level = (ProficiencyLevel)value;
    }
    
    private void OnDraftSuspendedChanged(ChangeEventArgs args) => Entry.IsSuspended = args.Value is bool b && b;

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