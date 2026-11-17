namespace LangNotebook.Helper;

public sealed class AppState
{
    private int? _activeNotebookId;
    public event Action? Changed;
    /// <summary>The Review item in the sidebar was clicked. The Review page returns to its setup screen.</summary>
    public event Action? ReviewMenuClicked;
    public event Action<string, string>? Toast;
    public int? ActiveNotebookId
    {
        get => _activeNotebookId;
        set
        {
            if (_activeNotebookId == value)
                return;
            _activeNotebookId = value;
            Changed?.Invoke();
        }
    }
    public void NotifyChanged() => Changed?.Invoke();

    public void RequestReviewReset() => ReviewMenuClicked?.Invoke();
    
    public void ShowToast(string message, string kind = "info") => Toast?.Invoke(message, kind);
    
    public void ShowSuccess(string message) => ShowToast(message, "success");
    
    public void ShowError(string message) => ShowToast(message, "error");
}