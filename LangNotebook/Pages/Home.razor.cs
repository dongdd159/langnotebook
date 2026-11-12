using LangNotebook.Dto;
using LangNotebook.Models;
using Microsoft.AspNetCore.Components;

namespace LangNotebook.Pages;

public partial class Home : ComponentBase, IDisposable
{
    private const double RingCircumference = 2 * Math.PI * 44;

    private static readonly string[] DayNames =
        ["Chủ Nhật", "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy"];

    private static readonly string[] WeekLabels = ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];

    // Literal class names so Tailwind picks them up when scanning *.razor.cs.
    private static readonly string[] HeatClasses =
    [
        "bg-hov",
        "bg-[color-mix(in_srgb,var(--nt-accent)_22%,transparent)]",
        "bg-[color-mix(in_srgb,var(--nt-accent)_45%,transparent)]",
        "bg-[color-mix(in_srgb,var(--nt-accent)_72%,transparent)]",
        "bg-nt-accent"
    ];

    private DashboardStats? _stats;
    private List<VocabEntry> _recentVocab = new();
    private List<WritingPiece> _recentPieces = new();
    private Dictionary<int, string> _notebookNames = new();
    private int _vocabDue;
    private int _grammarDue;
    private int _newCount;
    private int _goal = 30;

    private List<HeatCell> _heat = new();
    private List<WeekDay> _week = new();
    private List<Segment> _stages = new();
    private List<Segment> _grades = new();
    private int _heatTotal;
    private int _heatActiveDays;

    private int DueCount => _vocabDue + _grammarDue;
    private int TotalCards => (_stats?.TotalVocab ?? 0) + (_stats?.TotalGrammar ?? 0);
    private int ReviewsToday => _stats?.ReviewsToday ?? 0;
    private int GoalRemaining => Math.Max(0, _goal - ReviewsToday);
    private double GoalRatio => _goal <= 0 ? 0 : Math.Min(1, (double)ReviewsToday / _goal);
    private string RingOffset => (RingCircumference * (1 - GoalRatio)).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    private string RingDash => RingCircumference.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    private int GradeTotal => _grades.Sum(x => x.Count);

    private static string TodayLabel
    {
        get
        {
            var now = DateTime.Now;
            return $"{DayNames[(int)now.DayOfWeek]}, {now:dd/MM/yyyy}";
        }
    }

    private string? EstimateLabel
    {
        get
        {
            var queue = DueCount + _newCount;
            if (_stats is null || queue == 0 || _stats.AverageSecondsPerCard <= 0)
                return null;
            var minutes = (int)Math.Ceiling(queue * _stats.AverageSecondsPerCard / 60);
            return $"≈ {minutes} phút";
        }
    }

    protected override void OnInitialized() => State.Changed += OnStateChanged;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        var notebookId = State.ActiveNotebookId;
        _stats = await Stats.GetAsync(notebookId);
        (_vocabDue, _grammarDue, _newCount) = await Reviews.CountQueueByKindAsync(notebookId);
        var settings = await SettingsSvc.GetAsync();
        _goal = settings.DailyReviewGoal;
        _recentVocab = (await Library.GetVocabAsync(new ItemFilter
        {
            NotebookId = notebookId,
            SortBy = "recent"
        })).Take(5).ToList();
        _recentPieces = (await Writings.GetPiecesAsync(notebookId)).Take(3).ToList();
        _notebookNames = _stats.Notebooks.ToDictionary(x => x.NotebookId, x => x.Name);
        BuildViewModels(_stats);
    }

    private void BuildViewModels(DashboardStats stats)
    {
        var today = DateTime.Now.Date;

        var max = stats.Heatmap.Count == 0 ? 0 : stats.Heatmap.Max(x => x.Count);
        _heat = stats.Heatmap.Select(d => new HeatCell(d.Date, d.Count,
            d.Count == 0 || max == 0 ? 0 : Math.Clamp((int)Math.Ceiling(4.0 * d.Count / max), 1, 4))).ToList();
        _heatTotal = stats.Heatmap.Sum(x => x.Count);
        _heatActiveDays = stats.Heatmap.Count(x => x.Count > 0);

        var byDate = stats.Heatmap.ToDictionary(x => x.Date.Date, x => x.Count);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        _week = Enumerable.Range(0, 7).Select(i =>
        {
            var date = monday.AddDays(i);
            var done = byDate.TryGetValue(date, out var c) && c > 0;
            return new WeekDay(WeekLabels[i], done, date == today, date > today);
        }).ToList();

        _stages =
        [
            new Segment("Mới", stats.NewCount, "bg-nt-press"),
            new Segment("Đang học", stats.LearningCount, "bg-[#e8883a]"),
            new Segment("Mới thuộc", stats.YoungCount, "bg-[color-mix(in_srgb,var(--nt-accent)_45%,transparent)]"),
            new Segment("Đã thuộc", stats.MatureCount, "bg-nt-accent")
        ];

        int Grade(ReviewGrade g) => stats.GradeBreakdown.TryGetValue(g, out var c) ? c : 0;
        _grades =
        [
            new Segment("Quên", Grade(ReviewGrade.Again), "bg-nt-danger"),
            new Segment("Khó", Grade(ReviewGrade.Hard), "bg-[#e8883a]"),
            new Segment("Tốt", Grade(ReviewGrade.Good), "bg-nt-accent"),
            new Segment("Dễ", Grade(ReviewGrade.Easy), "bg-[#2fa36b]")
        ];
    }

    private static string HeatClass(int level) => HeatClasses[Math.Clamp(level, 0, 4)];

    private static string Pct(int part, int whole) =>
        whole <= 0 ? "0%" : (100.0 * part / whole).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";

    private string GradeWidth(int count)
    {
        var max = _grades.Count == 0 ? 0 : _grades.Max(x => x.Count);
        return Pct(count, max);
    }

    private static string Fmt(int value) => value.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');

    private static string FormatPercent(double value) => $"{Math.Round(value * 100)}%";

    private static string FormatSeconds(double value) =>
        value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + " giây";

    private static string RelativeDay(DateTime utc)
    {
        var date = utc.ToLocalTime().Date;
        var days = (DateTime.Now.Date - date).Days;
        return days switch
        {
            <= 0 => "Hôm nay",
            1 => "Hôm qua",
            < 7 => $"{days} ngày trước",
            _ => date.ToString("dd/MM/yyyy")
        };
    }

    private string NotebookName(int id) => _notebookNames.TryGetValue(id, out var name) ? name : string.Empty;

    private string PieceMeta(WritingPiece piece)
    {
        var parts = new List<string> { RelativeDay(piece.UpdatedUtc), $"{Fmt(piece.WordCount)} từ" };
        var notebook = NotebookName(piece.NotebookId);
        if (notebook.Length > 0)
            parts.Add(notebook);
        return string.Join(" · ", parts);
    }

    private static (string Label, string Css) StageTag(LearningStage stage) => stage switch
    {
        LearningStage.New => ("Mới", "nt-tag-0"),
        LearningStage.Learning => ("Đang học", "nt-tag-2"),
        LearningStage.Young => ("Mới thuộc", "nt-tag-5"),
        _ => ("Đã thuộc", "nt-tag-4")
    };

    private static (string Label, string Css) ScoreTag(WritingPiece piece)
    {
        if (piece.Status == WritingStatus.Draft)
            return ("Nháp", "nt-tag-0");
        var css = piece.AutoScore >= 80 ? "nt-tag-4" : piece.AutoScore >= 60 ? "nt-tag-3" : "nt-tag-8";
        return ($"{piece.AutoScore}/100", css);
    }

    private void OnStateChanged() => InvokeAsync(async () =>
    {
        await LoadAsync();
        StateHasChanged();
    });

    public void Dispose() => State.Changed -= OnStateChanged;

    private sealed record HeatCell(DateTime Date, int Count, int Level);

    private sealed record WeekDay(string Label, bool Done, bool IsToday, bool IsFuture);

    private sealed record Segment(string Label, int Count, string Css);
}
