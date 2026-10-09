namespace LangNotebook.Helper;

public sealed class AppState
{
    private int? _activeNotebookId;
    public event Action? Changed;
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
    
    public void ShowToast(string message, string kind = "info") => Toast?.Invoke(message, kind);
    
    public void ShowSuccess(string message) => ShowToast(message, "success");
    
    public void ShowError(string message) => ShowToast(message, "error");
}