using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using LangNotebook;
using LangNotebook.Data;
using LangNotebook.Helper;
using LangNotebook.Service;
using Microsoft.EntityFrameworkCore;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
// Deployment settings (AI providers and endpoints) come from wwwroot/config, loaded before anything uses them.
using (var configHttp = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) })
    AiProviders.Configure(await AppConfig.LoadAsync(configHttp));
builder.Services.AddDbContextFactory<ApplicationContext>(options =>
    options.UseSqlite($"Data Source={BrowserDbStorage.DbFilePath};Pooling=False"));
builder.Services.AddSingleton<BrowserDbStorage>();
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddSingleton<AppState>();
builder.Services.AddScoped<LibraryService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<WritingService>();
builder.Services.AddScoped<StatsService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<BackupService>();
builder.Services.AddScoped<CloudBackupService>();
builder.Services.AddScoped<AiSettingsStore>();
builder.Services.AddScoped<WritingAiService>();
await builder.Build().RunAsync();