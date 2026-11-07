using Microsoft.AspNetCore.Components;

namespace LangNotebook.Pages.Writing;

public partial class Writing : ComponentBase
{
    private static readonly Models.ProficiencyLevel[] Levels =
        (Models.ProficiencyLevel[])Enum.GetValues(typeof(Models.ProficiencyLevel));
    private List<Models.WritingPiece> _pieces = new();
    private List<Models.WritingPrompt> _prompts = new();
    private List<Models.Notebook> _notebooks = new();
    private string _search = string.Empty;

    private bool _promptEditorOpen;
    private Models.WritingPrompt _promptDraft = new();

    private bool CanSavePrompt => !string.IsNullOrWhiteSpace(_promptDraft.Title)
                                  && !string.IsNullOrWhiteSpace(_promptDraft.PromptText)
                                  && _promptDraft.NotebookId > 0;

    protected override void OnInitialized()
    {
        State.Changed += OnStateChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        _notebooks = await Library.GetNotebooksAsync();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _prompts = await Writings.GetPromptsAsync(State.ActiveNotebookId);
        _pieces = await Writings.GetPiecesAsync(State.ActiveNotebookId, _search);
    }

    private async Task OnSearchInput(ChangeEventArgs args)
    {
        _search = args.Value?.ToString() ?? string.Empty;
        _pieces = await Writings.GetPiecesAsync(State.ActiveNotebookId, _search);
    }

    private void OpenPromptEditor(Models.WritingPrompt? prompt)
    {
        if (_notebooks.Count == 0)
        {
            State.ShowError("Create at least a notebook.");
            return;
        }

        _promptDraft = prompt is null
            ? new Models.WritingPrompt { NotebookId = State.ActiveNotebookId ?? _notebooks[0].Id }
            : new Models.WritingPrompt
            {
                Id = prompt.Id,
                NotebookId = prompt.NotebookId,
                Title = prompt.Title,
                PromptText = prompt.PromptText,
                Topic = prompt.Topic,
                Level = prompt.Level,
                TargetWordCount = prompt.TargetWordCount,
                SuggestedTerms = prompt.SuggestedTerms,
                CreatedUtc = prompt.CreatedUtc
            };

        _promptEditorOpen = true;
    }

    private void OnPromptNotebookChanged(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var id))
        {
            _promptDraft.NotebookId = id;
        }
    }

    private void OnPromptLevelChanged(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var value))
        {
            _promptDraft.Level = (Models.ProficiencyLevel)value;
        }
    }

    private async Task SavePromptAsync()
    {
        if (!CanSavePrompt)
        {
            return;
        }

        await Writings.SavePromptAsync(_promptDraft);
        _promptEditorOpen = false;
        State.ShowSuccess("Saved.");
        await LoadAsync();
    }

    private async Task DeletePromptAsync(Models.WritingPrompt prompt)
    {
        var confirmed = await JS.InvokeAsync<bool>("confirm", [$"Clear the promt “{prompt.Title}”?"]);
        if (!confirmed)
        {
            return;
        }

        await Writings.DeletePromptAsync(prompt.Id);
        await LoadAsync();
    }

    private async Task DeletePieceAsync(Models.WritingPiece piece)
    {
        var confirmed = await JS.InvokeAsync<bool>("confirm", [$"Delete article “{piece.DisplayTitle}”?"]);
        if (!confirmed)
        {
            return;
        }

        await Writings.DeletePieceAsync(piece.Id);
        State.ShowSuccess("Deleted.");
        await LoadAsync();
    }

    private static string ScoreBucket(int score) => score switch
    {
        >= 85 => "high",
        >= 70 => "mid",
        >= 50 => "low",
        _ => "none"
    };

    private void OnStateChanged()
    {
        InvokeAsync(async () =>
        {
            await LoadAsync();
            StateHasChanged();
        });
    }

    public void Dispose()
    {
        State.Changed -= OnStateChanged;
    }
}