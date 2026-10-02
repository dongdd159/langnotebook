using LangNotebook.Dto;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyModel;

namespace LangNotebook.Pages;

public partial class Home : ComponentBase
{
    private DashboardStats? _stats;
    private List<Models.VocabEntry> _recentVocab = new();
    private List<Models.WritingPiece> _recentPieces = new();
    private int _dueCount;
    private int _newCount;
    private int _goal = 30;
    private int GoalPercent =>
        _goal <= 0 ? 0 : (int)Math.Min(100, Math.Round(100.0 * (_stats?.ReviewsToday ?? 0) / _goal));
    
    protected override void OnInitialized() => State.Changed += OnStateChanged;
    
    protected override async Task OnInitializedAsync() => await LoadAsync();
    
    private async Task LoadAsync()
    {
        var notebookId = State.ActiveNotebookId;
        _stats = await Stats.GetAsync(notebookId);
        var counts = await Reviews.CountQueueAsync(notebookId);
        _dueCount = counts.Due;
        _newCount = counts.New;
        var settings = await SettingsSvc.GetAsync();
        _goal = settings.DailyReviewGoal;
        _recentVocab = (await Library.GetVocabAsync(new ItemFilter
        {
            NotebookId = notebookId,
            SortBy = "recent"
        })).Take(5).ToList();
        _recentPieces = (await Writings.GetPiecesAsync(notebookId)).Take(5).ToList();
    }

    private void OnStateChanged() => InvokeAsync(async () =>
    {
        await LoadAsync();
        StateHasChanged();
    });

    private static string FormatPercent(double value) => $"{Math.Round(value * 100)}%";

    public void Dispose() => State.Changed -= OnStateChanged;
}