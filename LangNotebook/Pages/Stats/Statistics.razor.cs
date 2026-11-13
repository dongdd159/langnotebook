using System.Globalization;
using LangNotebook.Dto;
using LangNotebook.Models;
using Microsoft.AspNetCore.Components;

namespace LangNotebook.Pages.Stats;

public partial class Statistics : ComponentBase, IDisposable
{
    private DashboardStats? _stats;
    private int _goal = 30;
    private int _newCount;
    private List<Bar> _bars = new();
    private List<Segment> _stages = new();
    private List<Segment> _grades = new();
    private int _chartMax = 1;

    private int TotalCards => (_stats?.TotalVocab ?? 0) + (_stats?.TotalGrammar ?? 0);
    private int GradeTotal => _grades.Sum(x => x.Count);
    private int ReviewsLast30 => _bars.Sum(x => x.Count);

    // Goal line position as a percentage of the chart height.
    private string GoalLine => Pct(_goal, _chartMax);

    protected override void OnInitialized() => State.Changed += OnStateChanged;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        var notebookId = State.ActiveNotebookId;
        _stats = await StatsSvc.GetAsync(notebookId);
        _goal = (await SettingsSvc.GetAsync()).DailyReviewGoal;
        _newCount = (await Reviews.CountQueueAsync(notebookId)).New;
        Build(_stats);
    }

    private void Build(DashboardStats stats)
    {
        var max = stats.DailyReviews.Count == 0 ? 0 : stats.DailyReviews.Max(x => x.Count);
        _chartMax = Math.Max(1, Math.Max(max, _goal));
        var today = DateTime.Now.Date;
        _bars = stats.DailyReviews.Select(d => new Bar(
            d.Date,
            d.Count,
            Pct(d.Count, _chartMax),
            _goal > 0 && d.Count >= _goal,
            d.Date == today)).ToList();

        _stages =
        [
            new Segment("New", stats.NewCount, "bg-[#d8d4ca]"),
            new Segment("Learning", stats.LearningCount, "bg-[#e9a35a]"),
            new Segment("Young", stats.YoungCount, "bg-[#8fb0e3]"),
            new Segment("Mature", stats.MatureCount, "bg-nt-accent")
        ];

        int Grade(ReviewGrade g) => stats.GradeBreakdown.TryGetValue(g, out var c) ? c : 0;
        _grades =
        [
            new Segment("Again", Grade(ReviewGrade.Again), "bg-[#c0473a]"),
            new Segment("Hard", Grade(ReviewGrade.Hard), "bg-[#d58a2e]"),
            new Segment("Good", Grade(ReviewGrade.Good), "bg-nt-accent"),
            new Segment("Easy", Grade(ReviewGrade.Easy), "bg-[#2f7a55]")
        ];
    }

    private string StageWidth(int count)
    {
        var max = _stages.Count == 0 ? 0 : _stages.Max(x => x.Count);
        return Pct(count, max);
    }

    private string GradeHeight(int count)
    {
        var max = _grades.Count == 0 ? 0 : _grades.Max(x => x.Count);
        return Pct(count, max);
    }

    private static string Pct(int part, int whole) =>
        whole <= 0 ? "0%" : (100.0 * part / whole).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    private static string Share(int part, int whole) =>
        whole <= 0 ? "0%" : Math.Round(100.0 * part / whole).ToString(CultureInfo.InvariantCulture) + "%";

    private static string Fmt(int value) => value.ToString("#,0", CultureInfo.InvariantCulture);

    private static string FormatPercent(double value) => $"{Math.Round(value * 100)}%";

    private static string FormatSeconds(double value) =>
        value.ToString("0.#", CultureInfo.InvariantCulture) + " s";

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{Fmt(count)} {noun}s";

    private static string DayLabel(DateTime date) => date.ToString("MMM d", CultureInfo.InvariantCulture);

    private void OnStateChanged() => InvokeAsync(async () =>
    {
        await LoadAsync();
        StateHasChanged();
    });

    public void Dispose() => State.Changed -= OnStateChanged;

    private sealed record Bar(DateTime Date, int Count, string Height, bool ReachedGoal, bool IsToday);

    private sealed record Segment(string Label, int Count, string Css);
}
