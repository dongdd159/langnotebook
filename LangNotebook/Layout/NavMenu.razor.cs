using LangNotebook.Dto;
using LangNotebook.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

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

    private ElementReference _closeButton;

    // Chuỗi class tự động tính toán theo trạng thái Mobile Drawer & Desktop Collapse
    private string AsideClass => 
        $"{(DrawerOpen ? "translate-x-0 w-[240px]" : "-translate-x-full md:translate-x-0")} " +
        $"{(Collapsed ? "md:w-16" : "md:w-[240px]")}";

    private async Task CloseDrawerAsync()
    {
        DrawerOpen = false;
        await DrawerOpenChanged.InvokeAsync(DrawerOpen);
    }

    private async Task CollapseAsync()
    {
        Collapsed = !Collapsed;
        await CollapsedChanged.InvokeAsync(Collapsed);
    }

    private async Task SelectAsync(int? id)
    {
        await OnSelectNotebook.InvokeAsync(id);
        if (DrawerOpen)
        {
            await CloseDrawerAsync();
        }
    }

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape" && DrawerOpen)
        {
            await CloseDrawerAsync();
        }
    }
}