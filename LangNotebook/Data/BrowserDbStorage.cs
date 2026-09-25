using Microsoft.JSInterop;

namespace LangNotebook.Data;

public class BrowserDbStorage(IJSRuntime js)
{
    private const string DbFilePath = "/app.db";
    private bool _restored;
    
    public async Task RestoreAsync()
    {
        if (_restored)
            return;
        _restored = true;
        try
        {
            var base64 = await js.InvokeAsync<string?>("langDb.load");
            if (!string.IsNullOrEmpty(base64))
            {
                var bytes = Convert.FromBase64String(base64);
                await File.WriteAllBytesAsync(DbFilePath, bytes);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LangNotebook] Can't load Data: {ex.Message}");
        }
    }
    
    public async Task PersistAsync()
    {
        try
        {
            if (!File.Exists(DbFilePath))
                return;
            var bytes = await File.ReadAllBytesAsync(DbFilePath);
            await js.InvokeVoidAsync("langDb.save", Convert.ToBase64String(bytes));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Can't save Data: {ex.Message}");
        }
    }
    
    public async Task<byte[]> ReadRawAsync()
    {
        if (!File.Exists(DbFilePath))
            return Array.Empty<byte>();
        return await File.ReadAllBytesAsync(DbFilePath);
    }
    
    public async Task WriteRawAsync(byte[] bytes)
    {
        await File.WriteAllBytesAsync(DbFilePath, bytes);
        await js.InvokeVoidAsync("langDb.save", Convert.ToBase64String(bytes));
    }
    
    public async Task ClearAsync()
    {
        await js.InvokeVoidAsync("langDb.clear");
        if (File.Exists(DbFilePath))
            File.Delete(DbFilePath);
    }
}