using Microsoft.AspNetCore.Components;

namespace LangNotebook.Components;

public partial class Modal : ComponentBase
{
    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
    [Parameter] public string Title { get; set; } = string.Empty;
    [Parameter] public string Size { get; set; } = "md";
    [Parameter] public RenderFragment? ChildContent { get; set; }
    
    private string WidthClass => Size switch
    {
        "sm" => "max-w-sm",
        "lg" => "max-w-2xl",
        _ => "max-w-lg"
    };
    
    private async Task CloseAsync()
    {
        Visible = false;
        await VisibleChanged.InvokeAsync(Visible);
    }
}