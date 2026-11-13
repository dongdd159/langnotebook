using LangNotebook.Dto;
using LangNotebook.Helper;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace LangNotebook.Pages.Review;

public partial class Review : IDisposable
{
    private static readonly (Models.ReviewDirection Value, string Label)[] DirectionOptions =
    [
        (Models.ReviewDirection.TermToMeaning, "Term → Meaning"),
        (Models.ReviewDirection.MeaningToTerm, "Meaning → Term"),
        (Models.ReviewDirection.Mixed, "Mix both directions")
    ];
    private static readonly Models.ReviewScope[] Scopes = (Models.ReviewScope[])Enum.GetValues(typeof(Models.ReviewScope));
    private static readonly Models.ReviewGrade[] Grades = (Models.ReviewGrade[])Enum.GetValues(typeof(Models.ReviewGrade));
    private Phase _phase = Phase.Setup;
    private ElementReference _root;
    private List<ReviewCard> _queue = new();
    private int _index;
    private ReviewCard? _current;
    private bool _revealed;
    private bool _askReversed;
    private Models.ReviewScope _scope = Models.ReviewScope.All;
    private Models.ReviewDirection _direction = Models.ReviewDirection.TermToMeaning;
    private bool _includeNew = true;
    private readonly ReviewSessionStats _stats = new();
    private DateTime _cardShownUtc = DateTime.UtcNow;
    private DateTime _sessionStartUtc = DateTime.UtcNow;
    private int _elapsedSeconds;
    private int _dueCount;
    private int _newCount;
    private readonly Random _random = new();
    private int SessionPercent => _stats.Total == 0 ? 0 : (int)Math.Round(100.0 * _stats.Done / _stats.Total);
    private string FrontText => _current is null ? string.Empty : (_askReversed ? _current.Back : _current.Front);
    private string BackText => _current is null ? string.Empty : (_askReversed ? _current.Front : _current.Back);
    private readonly ItemFilter _filter = new();
    private List<string> _tags = new();

    protected override void OnInitialized() => State.Changed += OnStateChanged;

    protected override async Task OnInitializedAsync()
    {
        var settings = await SettingsSvc.GetAsync();
        _direction = settings.DefaultDirection;
        await LoadAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await _root.FocusAsync();
    }

    private async Task LoadAsync()
    {
        await RefreshCountsAsync();
        _tags = await Library.GetTagsAsync(State.ActiveNotebookId);
    }
    
    private async Task RefreshCountsAsync()
    {
        var counts = await Reviews.CountQueueAsync(State.ActiveNotebookId);
        _dueCount = counts.Due;
        _newCount = counts.New;
    }

    private void OnScopeChanged(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var value))
            _scope = (Models.ReviewScope)value;
    }

    private void OnDirectionChanged(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var value))
            _direction = (Models.ReviewDirection)value;
    }

    private void OnIncludeNewChanged(ChangeEventArgs args) => _includeNew = args.Value is bool b && b;

    private async Task StartAsync()
    {
        var settings = await SettingsSvc.GetAsync();
        _filter.NotebookId = State.ActiveNotebookId;
        _queue = await Reviews.BuildSessionAsync(_scope, _filter, settings, _includeNew);
        if (_queue.Count == 0)
        {
            _phase = Phase.Empty;
            return;
        }
        _index = 0;
        _stats.Total = _queue.Count;
        _stats.Done = 0;
        _stats.Again = 0;
        _stats.Hard = 0;
        _stats.Good = 0;
        _stats.Easy = 0;
        _sessionStartUtc = DateTime.UtcNow;
        _phase = Phase.Session;
        ShowCard();
        await _root.FocusAsync();
    }

    private void ShowCard()
    {
        if (_index >= _queue.Count)
        {
            FinishSession();
            return;
        }
        _current = _queue[_index];
        _revealed = false;
        _cardShownUtc = DateTime.UtcNow;
        _askReversed = _direction switch
        {
            Models.ReviewDirection.MeaningToTerm => true,
            Models.ReviewDirection.Mixed => _random.Next(2) == 0,
            _ => false
        };
        if (_current.Kind == Models.StudyItemKind.Grammar)
            _askReversed = false;
    }

    private void Reveal() => _revealed = true;

    private async Task GradeAsync(Models.ReviewGrade grade)
    {
        if (_current is null || !_revealed)
            return;
        var duration = (int)(DateTime.UtcNow - _cardShownUtc).TotalMilliseconds;
        await Reviews.GradeAsync(_current, grade, Math.Clamp(duration, 0, 600000));
        _stats.Done++;
        switch (grade)
        {
            case Models.ReviewGrade.Again:
                _stats.Again++;
                _queue.Add(_current);
                _stats.Total++;
                break;
            case Models.ReviewGrade.Hard:
                _stats.Hard++;
                break;
            case Models.ReviewGrade.Good:
                _stats.Good++;
                break;
            default:
                _stats.Easy++;
                break;
        }
        _index++;
        ShowCard();
        await LoadAsync();
        State.NotifyChanged();
    }

    private void FinishSession()
    {
        _elapsedSeconds = (int)(DateTime.UtcNow - _sessionStartUtc).TotalSeconds;
        _phase = Phase.Done;
        _current = null;
    }

    private void ResetToSetup()
    {
        _phase = Phase.Setup;
        _current = null;
        _queue.Clear();
    }

    private async Task OnKeyDown(KeyboardEventArgs args)
    {
        if (_phase != Phase.Session)
            return;
        if (!_revealed && (args.Key == " " || args.Key == "Enter"))
        {
            Reveal();
            return;
        }
        if (!_revealed)
            return;
        switch (args.Key)
        {
            case "1":
                await GradeAsync(Models.ReviewGrade.Again);
                break;
            case "2":
                await GradeAsync(Models.ReviewGrade.Hard);
                break;
            case "3":
            case " ":
            case "Enter":
                await GradeAsync(Models.ReviewGrade.Good);
                break;
            case "4":
                await GradeAsync(Models.ReviewGrade.Easy);
                break;
        }
    }
    
    private async Task OnTagChanged(ChangeEventArgs args) => _filter.Tag = args.Value?.ToString() ?? string.Empty;

    private void OnStateChanged() => InvokeAsync(async () =>
    {
        if (_phase == Phase.Setup)
        {
            await LoadAsync();
            StateHasChanged();
        }
    });

    public void Dispose() => State.Changed -= OnStateChanged;
}