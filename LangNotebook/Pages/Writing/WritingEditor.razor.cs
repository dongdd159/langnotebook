using LangNotebook.Dto;
using LangNotebook.Helper;
using LangNotebook.Models;
using LangNotebook.Service;
using Microsoft.AspNetCore.Components;

namespace LangNotebook.Pages.Writing;

public partial class WritingEditor : ComponentBase
{
    [Parameter] 
    public int Id { get; set; }
    [Parameter]
    [SupplyParameterFromQuery(Name = "promptId")]
    public int? PromptId { get; set; }
    private static readonly int[] Scores = { 0, 1, 2, 3, 4, 5 };
    private WritingPiece _piece = new();
    private WritingPrompt? _prompt;
    private WritingAnalysis _analysis = WritingAnalyzer.Analyze(string.Empty, 150);
    private List<string> _targetTerms = new();
    private string _targetTermsRaw = string.Empty;
    private List<VocabEntry> _suggestions = new();
    private int _elapsedSeconds;
    private CancellationTokenSource? _timerCts;
    private WritingAiReview? _aiReview;
    private AiReviewException? _aiError;
    private bool _aiBusy;
    private CancellationTokenSource? _aiCts;

    private bool CanEvaluate => !_aiBusy && !string.IsNullOrWhiteSpace(_piece.Content);

    protected override async Task OnParametersSetAsync()
    {
        if (Id > 0)
        {
            var existing = await Writings.GetPieceAsync(Id);
            if (existing is not null)
            {
                _piece = existing;
                _elapsedSeconds = existing.DurationSeconds;
                _targetTermsRaw = existing.UsedTerms;
                _aiReview = WritingAiReviewMapper.Deserialize(existing.AiReviewJson);
                if (existing.PromptId.HasValue)
                    _prompt = await Writings.GetPromptAsync(existing.PromptId.Value);
            }
        }
        else if (_piece.Id == 0 && string.IsNullOrEmpty(_piece.Content))
        {
            var notebooks = await Library.GetNotebooksAsync();
            var notebookId = State.ActiveNotebookId ?? notebooks.FirstOrDefault()?.Id ?? 0;
            _piece = new WritingPiece
            {
                NotebookId = notebookId,
                TargetWordCount = 150
            };
            if (PromptId.HasValue)
            {
                _prompt = await Writings.GetPromptAsync(PromptId.Value);
                if (_prompt is not null)
                {
                    _piece.PromptId = _prompt.Id;
                    _piece.NotebookId = _prompt.NotebookId;
                    _piece.TargetWordCount = _prompt.TargetWordCount;
                    _piece.Title = _prompt.Title;
                    _targetTermsRaw = _prompt.SuggestedTerms;
                }
            }
        }
        SyncTargetTerms();
        if (_piece.NotebookId > 0)
            _suggestions = await Library.GetSuggestedVocabAsync(_piece.NotebookId);
        Recalculate();
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
            StartTimer();
    }

    private void StartTimer()
    {
        _timerCts = new CancellationTokenSource();
        var token = _timerCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(1000, token);
                    _elapsedSeconds++;
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (TaskCanceledException)
            {
            }
        });
    }

    private void OnContentInput(ChangeEventArgs args)
    {
        _piece.Content = args.Value?.ToString() ?? string.Empty;
        Recalculate();
    }

    private void OnTargetTermsInput(ChangeEventArgs args)
    {
        _targetTermsRaw = args.Value?.ToString() ?? string.Empty;
        SyncTargetTerms();
        Recalculate();
    }

    private void AddTargetTerm(string term)
    {
        if (_targetTerms.Contains(term, StringComparer.OrdinalIgnoreCase))
            return;
        _targetTermsRaw = string.IsNullOrWhiteSpace(_targetTermsRaw) ? term : $"{_targetTermsRaw}, {term}";
        SyncTargetTerms();
        Recalculate();
    }

    private void SyncTargetTerms() => _targetTerms = _targetTermsRaw
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private void Recalculate() =>
        _analysis = WritingAnalyzer.Analyze(_piece.Content, _piece.TargetWordCount, _targetTerms);

    private static int ParseScore(ChangeEventArgs args) =>
        int.TryParse(args.Value?.ToString(), out var value) ? Math.Clamp(value, 0, 5) : 0;

    private void OnScoreTaskChanged(ChangeEventArgs args) => _piece.ScoreTask = ParseScore(args);

    private void OnScoreCoherenceChanged(ChangeEventArgs args) => _piece.ScoreCoherence = ParseScore(args);

    private void OnScoreVocabularyChanged(ChangeEventArgs args) => _piece.ScoreVocabulary = ParseScore(args);

    private void OnScoreGrammarChanged(ChangeEventArgs args) => _piece.ScoreGrammar = ParseScore(args);

    private async Task SaveAsync()
    {
        Recalculate();
        _piece.AutoScore = _analysis.TotalScore;
        _piece.DurationSeconds = _elapsedSeconds;
        _piece.UsedTerms = _targetTermsRaw;
        _piece.AiReviewJson = _aiReview is null ? string.Empty : WritingAiReviewMapper.Serialize(_aiReview);
        if (string.IsNullOrWhiteSpace(_piece.Title))
            _piece.Title = _prompt?.Title ?? $"Article {DateTime.Now:dd/MM HH:mm}";
        var id = await Writings.SavePieceAsync(_piece);
        State.ShowSuccess("Saved.");
        if (Id == 0 && id > 0)
            Nav.NavigateTo($"writing/{id}");
    }

    private async Task AnalyzeAndSaveAsync()
    {
        Recalculate();
        await SaveAsync();
    }

    private async Task EvaluateWithAiAsync()
    {
        if (!CanEvaluate)
            return;
        _aiBusy = true;
        _aiError = null;
        using var cts = new CancellationTokenSource();
        _aiCts = cts;
        try
        {
            _aiReview = await WritingAi.ReviewAsync(new AiReviewRequest(
                _piece.Content,
                _piece.TargetWordCount,
                _targetTerms,
                _prompt?.Title,
                _prompt?.PromptText,
                _prompt?.Level.ToString()), cts.Token);
            _aiBusy = false;
            await SaveAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (AiReviewException ex)
        {
            _aiError = ex;
        }
        finally
        {
            _aiBusy = false;
            _aiCts = null;
        }
    }

    private void CancelAiReview() => _aiCts?.Cancel();

    private void ApplyAiChanges(string improvedText)
    {
        _piece.Content = improvedText;
        Recalculate();
        State.ShowSuccess("Changes applied. Save the article to keep them.");
    }

    private async Task ToggleStatusAsync()
    {
        _piece.Status = _piece.Status == WritingStatus.Draft ? WritingStatus.Finished : WritingStatus.Draft;
        await SaveAsync();
    }

    private static string ScoreBucket(int score) => score switch
    {
        >= 85 => "high",
        >= 70 => "mid",
        >= 50 => "low",
        _ => "none"
    };

    public ValueTask DisposeAsync()
    {
        _timerCts?.Cancel();
        _timerCts?.Dispose();
        _timerCts = null;
        _aiCts?.Cancel();
        return ValueTask.CompletedTask;
    }
}