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

    private async Task SelectAsync(int? id)
    {
        await OnSelectNotebook.InvokeAsync(id);
        if (DrawerOpen)
            await CloseDrawerAsync();
        if (!Collapsed)
            await CollapseAsync();
    }

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape" && DrawerOpen)
            await CloseDrawerAsync();
    }
}