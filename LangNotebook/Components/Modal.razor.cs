using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace LangNotebook.Components;

public partial class Modal : ComponentBase
{
    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
    [Parameter] public string Title { get; set; } = string.Empty;
    [Parameter] public string Size { get; set; } = "md";
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private ElementReference _dialog;
    private bool _wasVisible;
    
    private string WidthClass => Size switch
    {
        "sm" => "max-w-sm",
        "lg" => "max-w-2xl",
        _ => "max-w-lg"
    };
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Visible == _wasVisible)
            return;
        _wasVisible = Visible;
        // Focus moves into the dialog when it opens and back to what opened it when it closes.
        if (Visible)
            await JS.InvokeVoidAsync("langDialog.open", _dialog);
        else
            await JS.InvokeVoidAsync("langDialog.close");
    }

    private async Task OnKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
            await CloseAsync();
    }

    private async Task CloseAsync()
    {
        Visible = false;
        await VisibleChanged.InvokeAsync(Visible);
    }
}