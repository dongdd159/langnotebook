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
    [Parameter] public List<Models.Notebook> Notebooks { get; set; } = new();
    [Parameter] public int? ActiveNotebookId { get; set; }
    [Parameter] public int DueCount { get; set; }
    [Parameter] public EventCallback<int?> OnSelectNotebook { get; set; }
    private ElementReference _closeButton;
    private bool _wasOpen;
    private string AsideClass =>
        "fixed inset-y-0 left-0 z-40 flex w-[85vw] max-w-[300px] shrink-0 flex-col border-r border-line bg-nt-side " +
        "transition-[transform,visibility] duration-200 ease-out motion-reduce:transition-none " +
        "md:static md:w-side md:max-w-none md:translate-x-0 md:visible " +
        (DrawerOpen ? "translate-x-0 visible shadow-pop md:shadow-none " : "-translate-x-full invisible ") +
        (Collapsed ? "md:hidden" : string.Empty);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (DrawerOpen && !_wasOpen)
            try
            {
                await _closeButton.FocusAsync();
            }
            catch (InvalidOperationException)
            {
            }
        _wasOpen = DrawerOpen;
    }

    private async Task CloseDrawerAsync()
    {
        if (DrawerOpen)
            await DrawerOpenChanged.InvokeAsync(false);
    }

    private async Task CollapseAsync()
    {
        await DrawerOpenChanged.InvokeAsync(false);
        await CollapsedChanged.InvokeAsync(true);
    }

    private async Task SelectAsync(int? id)
    {
        await OnSelectNotebook.InvokeAsync(id);
        await CloseDrawerAsync();
    }

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
            await CloseDrawerAsync();
    }
}