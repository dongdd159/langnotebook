namespace LangNotebook.Dto;

public class ToastItem
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Message { get; init; } = string.Empty;
    public string Kind { get; init; } = "info";
}