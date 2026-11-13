using LangNotebook.Dto;
using LangNotebook.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LangNotebook.Layout;

public partial class MainLayout : LayoutComponentBase
{
    private readonly List<ToastItem> _toasts = new();
    private List<Notebook> _notebooks = new();
    private bool _ready;
    private bool _drawerOpen;
    public bool DrawerOpen
    {
        get => _drawerOpen;
        set
        {
            _drawerOpen = value;
        }
    }
    private bool _collapsed;
    public bool Collapsed
    {
        get => _collapsed;
        set
        {
            _collapsed = value;
        }
    }
    private bool _dark;
    private int _dueCount;
    private string _selectedNotebookValue = string.Empty;
    private string AsideClass => $"{(_drawerOpen ? "translate-x-0" : "-translate-x-full md:translate-x-0")} " +
                                 $"{(_collapsed ? "w-[240px] md:w-16" : "md:w-[240px]")}";
    private string ActiveNotebookName =>
        _notebooks.FirstOrDefault(x => x.Id == State.ActiveNotebookId)?.Name ?? "All Notebooks";
    private (string Title, string Icon) CurrentPage
    {
        get
        {
            var path = Nav.ToBaseRelativePath(Nav.Uri).Split('?', '#')[0].Trim('/');
            var first = path.Split('/')[0];
            return first switch
            {
                "" => ("Overview", "home"),
                "review" => ("Review", "review"),
                "vocabulary" => ("Vocabulary", "book"),
                "grammar" => ("Grammar & Structures", "grammar"),
                "writing" => ("Writing Practice", "pen"),
                "stats" => ("Statistics", "chart"),
                "notebooks" => ("Manage Notebooks", "database"),
                "settings" => ("Settings", "settings"),
                _ => ("Language Notebook", "book")
            };
        }
    }

    protected override void OnInitialized()
    {
        State.Changed += OnStateChanged;
        State.Toast += OnToast;
        Nav.LocationChanged += OnLocationChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;
        _dark = await JS.InvokeAsync<string>("langTheme.mode") == "dark";
        await Initializer.EnsureReadyAsync();
        var settings = await SettingsSvc.GetAsync();
        State.ActiveNotebookId = settings.ActiveNotebookId;
        _selectedNotebookValue = settings.ActiveNotebookId?.ToString() ?? string.Empty;
        await ReloadAsync();
        _ready = true;
        StateHasChanged();
    }

    private async Task ReloadAsync()
    {
        _notebooks = await Library.GetNotebooksAsync();
        var counts = await Reviews.CountQueueAsync(State.ActiveNotebookId);
        _dueCount = counts.Due;
    }

    private async Task ToggleThemeAsync()
    {
        // Read the current mode first: Settings can change the theme without this component knowing.
        _dark = await JS.InvokeAsync<string>("langTheme.mode") == "dark";
        var next = _dark ? "light" : "dark";
        await JS.InvokeVoidAsync("langTheme.set", next);
        _dark = next == "dark";
    }

    private async Task OnNotebookChanged(ChangeEventArgs args)
    {
        var raw = args.Value?.ToString() ?? string.Empty;
        await SelectNotebookAsync(string.IsNullOrEmpty(raw) ? null : int.Parse(raw));
    }

    private async Task SelectNotebookAsync(int? id)
    {
        _selectedNotebookValue = id?.ToString() ?? string.Empty;
        State.ActiveNotebookId = id;
        var settings = await SettingsSvc.GetAsync();
        settings.ActiveNotebookId = id;
        await SettingsSvc.SaveAsync(settings);
        await ReloadAsync();
        StateHasChanged();
    }

    private void OnStateChanged()
    {
        InvokeAsync(async () =>
        {
            if (_ready)
                await ReloadAsync();
            StateHasChanged();
        });
    }

    private void OnLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        _drawerOpen = false;
        InvokeAsync(StateHasChanged);
    }

    private void OnToast(string message, string kind)
    {
        var item = new ToastItem { Message = message, Kind = kind };
        _toasts.Add(item);
        StateHasChanged();
        _ = HideLaterAsync(item);
    }

    private async Task HideLaterAsync(ToastItem item)
    {
        await Task.Delay(3200);
        _toasts.Remove(item);
        await InvokeAsync(StateHasChanged);
    }

    private void OpenDrawer() => _drawerOpen = true;

    private void CloseDrawer() => _drawerOpen = false;

    private void ExpandSidebar() => _collapsed = false;

    public void Dispose()
    {
        State.Changed -= OnStateChanged;
        State.Toast -= OnToast;
        Nav.LocationChanged -= OnLocationChanged;
    }
}