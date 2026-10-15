using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace LangNotebook.Components;

public partial class ComboxInput : ComponentBase
{
    [Parameter] 
    public string? Value { get; set; }
    [Parameter] 
    public EventCallback<string?> ValueChanged { get; set; }
    [Parameter] 
    public IEnumerable<string>? Options { get; set; }
    [Parameter] 
    public string? Placeholder { get; set; }
    [Parameter] 
    public bool AllowCreate { get; set; } = true;
    [Parameter] 
    public EventCallback<string> OnCreate { get; set; }
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? Attributes { get; set; }
    private readonly string _listId = "tg-" + Guid.NewGuid().ToString("N")[..8];
    private readonly List<string> _created = new();
    private ElementReference _input;
    private string _text = string.Empty;
    private bool _open;
    private int _active = -1;

    private List<string> Selected() => (Value ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private List<string> AllOptions() => (Options ?? Enumerable.Empty<string>()).Concat(_created)
        .Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private List<string> GetRows(List<string> selected)
    {
        var query = _text.Trim();
        return AllOptions().Where(o => !selected.Contains(o, StringComparer.OrdinalIgnoreCase))
            .Where(o => query.Length == 0 || o.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private static int Tone(string tag) => tag.Trim().ToLowerInvariant().Sum(c => (int)c) % 9;

    private ValueTask FocusInputAsync() => _input.FocusAsync();

    private void OnFocus()
    {
        _open = true;
        _active = -1;
    }

    private async Task OnBlurAsync()
    {
        _open = false;
        _active = -1;
        if (_text.Trim().Length > 0)
            await AddAsync(_text, keepOpen: false);
    }

    private async Task OnInput(ChangeEventArgs e)
    {
        var text = e.Value?.ToString() ?? string.Empty;
        _open = true;
        var cut = text.LastIndexOf(',');
        if (cut >= 0)
        {
            var done = text[..cut];
            _text = text[(cut + 1)..].TrimStart();
            await AddAsync(done, remaining: _text);
            return;
        }
        _text = text;
        _active = -1;
    }

    private async Task OnKeyDownAsync(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "ArrowDown":
                MoveActive(1);
                break;
            case "ArrowUp":
                MoveActive(-1);
                break;
            case "Enter":
                await EnterAsync();
                break;
            case "Backspace" when _text.Length == 0:
                var selected = Selected();
                if (selected.Count > 0)
                    await RemoveAsync(selected[^1]);
                break;
            case "Escape":
                _open = false;
                _active = -1;
                _text = string.Empty;
                break;
        }
    }
    
    private int RowCount()
    {
        var rows = GetRows(Selected());
        return rows.Count;
    }

    private void MoveActive(int delta)
    {
        if (!_open)
        {
            _open = true;
            return;
        }
        var count = RowCount();
        if (count == 0)
            return;
        _active = _active < 0 ? (delta > 0 ? 0 : count - 1) : (_active + delta + count) % count;
    }

    private async Task EnterAsync()
    {
        var rows = GetRows(Selected());
        if (_open && _active >= 0 && _active < rows.Count)
            await AddAsync(rows[_active]);
        else if (_text.Trim().Length > 0)
            await AddAsync(_text);
    }

    private async Task AddAsync(string raw, bool keepOpen = true, string? remaining = null)
    {
        var current = Selected();
        var options = AllOptions();
        var changed = false;
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var tag = options.FirstOrDefault(o => string.Equals(o, part, StringComparison.OrdinalIgnoreCase));
            if (tag is null)
            {
                if (!AllowCreate)
                    continue;
                tag = part;
                _created.Add(tag);
                options.Add(tag);
                await OnCreate.InvokeAsync(tag);
            }
            if (!current.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                current.Add(tag);
                changed = true;
            }
        }
        _text = remaining ?? string.Empty;
        _active = -1;
        _open = keepOpen;
        if (changed)
            await SetValueAsync(current);
    }

    private async Task RemoveAsync(string tag)
    {
        var current = Selected();
        current.RemoveAll(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
        await SetValueAsync(current);
    }

    private async Task SetValueAsync(List<string> tags)
    {
        Value = string.Join(", ", tags);
        await ValueChanged.InvokeAsync(Value);
    }
}