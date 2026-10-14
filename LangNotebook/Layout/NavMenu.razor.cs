using LangNotebook.Dto;
using LangNotebook.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace LangNotebook.Layout;

public partial class NavMenu
{
    [Parameter] public bool DrawerOpen { get; set; }
    [Parameter] public EventCallback<bool> DrawerOpenChanged { get; set; }
    [Parameter] public bool Collapsed { get; set; }
    [Parameter] public EventCallback<bool> CollapsedChanged { get; set; }
    [Parameter] public IEnumerable<Notebook> Notebooks { get; set; } = [];
    [Parameter] public int? ActiveNotebookId { get; set; }
    [Parameter] public int DueCount { get; set; }
    [Parameter] public EventCallback<int?> OnSelectNotebook { get; set; }
    [Parameter] public EventCallback OnReload { get; set; }
    public string InputValue = string.Empty;
    public bool _openEdit = false;
    private bool _loading = false;
    private bool _isSaving = false;
    private string _tempValue = string.Empty;
    private ElementReference _inputNotebook;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if(_openEdit)
            await _inputNotebook.FocusAsync();
    }
    
    private async Task CloseDrawerAsync()
    {
        DrawerOpen = false;
        await DrawerOpenChanged.InvokeAsync(DrawerOpen);
    }

    private async Task CollapseAsync()
    {
        Collapsed = true;
        await CollapsedChanged.InvokeAsync(Collapsed);
    }
    
    private async Task LoadAsync()
    {
        _loading = true;
        await OnReload.InvokeAsync();
        _openEdit = false;
        _loading = false;
    }

    private async Task SelectAsync(int? id)
    {
        await OnSelectNotebook.InvokeAsync(id);
        if (DrawerOpen)
            await CloseDrawerAsync();
        if (!Collapsed)
            await CollapseAsync();
    }

    private async Task AddNewNotebook()
    {
        DrawerOpen = true;
        await DrawerOpenChanged.InvokeAsync(DrawerOpen);
        Collapsed = false;
        await CollapsedChanged.InvokeAsync(Collapsed);
        _openEdit = true;
    }
    
    private void OnInputChanged(ChangeEventArgs e) => _tempValue = e.Value?.ToString() ?? string.Empty;

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await SaveChangesAsync();
            await JS.InvokeVoidAsync("eval", "document.activeElement.blur()");
        }
    }
    
    private async Task DeleteNotebookAsync(int notebookId)
    {
        var linkedList = new LinkedList<Notebook>(Notebooks);
        var targetNotebook = Notebooks.FirstOrDefault(x => x.Id == notebookId);
        if (targetNotebook != null)
        {
            var current = linkedList.Find(targetNotebook);
            if (current != null)
            {
                var next = current.Next ?? linkedList.First;
                await Library.DeleteNotebookAsync(notebookId);
                OnStateChanged();
                if(next != null)
                    await OnSelectNotebook.InvokeAsync(next.Value.Id);
            }
        }
        
    }
    
    private async Task OnBlur() => await SaveChangesAsync();

    private async Task SaveChangesAsync()
    {
        if (_isSaving) return;
        _isSaving = true;
        if (_tempValue != InputValue)
        {
            var newNotebookId = await Library.SaveNotebookAsync(new Notebook
            {
                Name = _tempValue
            });
            OnStateChanged();
            await OnSelectNotebook.InvokeAsync(newNotebookId);
        }

        _tempValue = String.Empty;
        _isSaving = false;  
        _openEdit = false;
    }
    
    private void OnStateChanged()
    {
        InvokeAsync(async () =>
        {
            await LoadAsync();
            State.ActiveNotebookId = ActiveNotebookId;
            State.NotifyChanged();
            StateHasChanged();
        });
    }
}