using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace LangNotebook.Pages;

public partial class Settings : ComponentBase
{
    private Models.AppSettings _settings = new();
    private bool _replaceOnImport = true;
    private string _importMessage = string.Empty;

    protected override async Task OnInitializedAsync() => _settings = await SettingsSvc.GetAsync();

    private void OnDirectionChanged(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var value))
            _settings.DefaultDirection = (Models.ReviewDirection)value;
    }

    private void OnReplaceChanged(ChangeEventArgs args) => _replaceOnImport = args.Value is bool b && b;

    private async Task SaveSettingsAsync()
    {
        await SettingsSvc.SaveAsync(_settings);
        State.ShowSuccess("Đã lưu cài đặt.");
        State.NotifyChanged();
    }

    private async Task ExportJsonAsync()
    {
        await Backup.DownloadJsonAsync();
        State.ShowSuccess("Đã tạo file sao lưu JSON.");
    }

    private async Task ExportDbAsync()
    {
        await Backup.DownloadDatabaseAsync();
        State.ShowSuccess("Đã tạo file sao lưu .db.");
    }

    private async Task OnFileSelected(InputFileChangeEventArgs args)
    {
        var file = args.File;
        if (file is null)
            return;
        try
        {
            const long maxSize = 64L * 1024 * 1024;
            await using var stream = file.OpenReadStream(maxSize);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            var bytes = memory.ToArray();
            if (file.Name.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            {
                await Backup.RestoreDatabaseAsync(bytes);
                _importMessage = "Đã khôi phục cơ sở dữ liệu. Hãy tải lại trang để thấy dữ liệu mới.";
            }
            else
            {
                var json = System.Text.Encoding.UTF8.GetString(bytes);
                var count = await Backup.ImportJsonAsync(json, _replaceOnImport);
                _importMessage = $"Đã nạp {count} bản ghi từ file JSON.";
            }
            State.ShowSuccess("Khôi phục xong.");
            State.NotifyChanged();
        }
        catch (Exception ex)
        {
            _importMessage = $"Không đọc được file: {ex.Message}";
            State.ShowError("Khôi phục thất bại.");
        }
    }

    private async Task WipeAsync()
    {
        var confirmed = await JS.InvokeAsync<bool>("confirm",
            args: ["Xoá toàn bộ dữ liệu học tập trên máy này? Hãy chắc chắn bạn đã sao lưu."]);
        if (!confirmed)
            return;
        await Backup.WipeAsync();
        _settings = await SettingsSvc.GetAsync();
        State.ActiveNotebookId = null;
        State.ShowSuccess("Đã xoá sạch dữ liệu.");
        State.NotifyChanged();
    }
}