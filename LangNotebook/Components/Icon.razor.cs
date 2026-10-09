using Microsoft.AspNetCore.Components;

namespace LangNotebook.Components;

public partial class Icon : ComponentBase
{
    [Parameter] public string Name { get; set; } = "book";
    [Parameter] public int Size { get; set; } = 16;
    [Parameter] public bool Fill { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;
    private string Path => Name switch
    {
        "home" => "<path d='M3 10.5 12 3l9 7.5'/><path d='M5 9.5V21h14V9.5'/><path d='M10 21v-6h4v6'/>",
        "review" => "<path d='M21 12a9 9 0 1 1-3-6.7'/><path d='M21 4v5h-5'/>",
        "book" => "<path d='M4 4.5A2.5 2.5 0 0 1 6.5 2H20v17H6.5A2.5 2.5 0 0 0 4 21.5z'/><path d='M4 21.5A2.5 2.5 0 0 0 6.5 24H20v-5'/>",
        "grammar" => "<path d='M4 7V5h16v2'/><path d='M12 5v14'/><path d='M9 19h6'/>",
        "pen" => "<path d='M12 20h9'/><path d='M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4z'/>",
        "chart" => "<path d='M4 20V10'/><path d='M10 20V4'/><path d='M16 20v-7'/><path d='M22 20H2'/>",
        "library" => "<path d='M4 4v16'/><path d='M9 4v16'/><path d='m14 5 4-1 3 15-4 1z'/>",
        "settings" => "<circle cx='12' cy='12' r='3'/><path d='M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z'/>",
        "search" => "<circle cx='11' cy='11' r='7'/><path d='m21 21-4.3-4.3'/>",
        "plus" => "<path d='M12 5v14'/><path d='M5 12h14'/>",
        "star" => "<path d='m12 2 3.1 6.3 6.9 1-5 4.9 1.2 6.8L12 17.8 5.8 21l1.2-6.8-5-4.9 6.9-1z'/>",
        "edit" => "<path d='M12 20h9'/><path d='M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4z'/>",
        "trash" => "<path d='M3 6h18'/><path d='M8 6V4h8v2'/><path d='m19 6-1 14H6L5 6'/><path d='M10 11v5'/><path d='M14 11v5'/>",
        "reset" => "<path d='M3 12a9 9 0 1 0 3-6.7'/><path d='M3 4v5h5'/>",
        "menu" => "<path d='M4 6h16'/><path d='M4 12h16'/><path d='M4 18h16'/>",
        "panel" => "<rect x='3' y='4' width='18' height='16' rx='2'/><path d='M9 4v16'/><path d='m13 9 3 3-3 3'/>",
        "close-panel" => "<rect x='3' y='4' width='18' height='16' rx='2'/><path d='M9 4v16'/><path d='m15 9-3 3 3 3'/>",
        "table" => "<rect x='3' y='4' width='18' height='16' rx='2'/><path d='M3 10h18'/><path d='M9 10v10'/>",
        "grid" => "<rect x='3' y='3' width='7' height='7' rx='1'/><rect x='14' y='3' width='7' height='7' rx='1'/><rect x='3' y='14' width='7' height='7' rx='1'/><rect x='14' y='14' width='7' height='7' rx='1'/>",
        "upload" => "<path d='M12 16V4'/><path d='m7 9 5-5 5 5'/><path d='M4 20h16'/>",
        "close" => "<path d='M18 6 6 18'/><path d='m6 6 12 12'/>",
        "filter" => "<path d='M3 5h18l-7 8v6l-4 2v-8z'/>",
        "sort" => "<path d='M7 4v16'/><path d='m3 16 4 4 4-4'/><path d='M17 20V4'/><path d='m13 8 4-4 4 4'/>",
        "clock" => "<circle cx='12' cy='12' r='9'/><path d='M12 7v5l3 2'/>",
        "database" => "<ellipse cx='12' cy='5' rx='8' ry='3'/><path d='M4 5v14c0 1.7 3.6 3 8 3s8-1.3 8-3V5'/><path d='M4 12c0 1.7 3.6 3 8 3s8-1.3 8-3'/>",
        "type" => "<path d='M4 7V5h16v2'/><path d='M12 5v14'/><path d='M9 19h6'/>",
        "hash" => "<path d='M4 9h16'/><path d='M4 15h16'/><path d='m10 3-2 18'/><path d='m16 3-2 18'/>",
        "chevron" => "<path d='m9 6 6 6-6 6'/>",
        "chevrons-left" => "<path d='m11 17-5-5 5-5'/><path d='m18 17-5-5 5-5'/>",
        _ => "<circle cx='12' cy='12' r='9'/>"
    };
}