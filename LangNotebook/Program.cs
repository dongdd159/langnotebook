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
builder.Services.AddScoped<AiSettingsStore>();
builder.Services.AddScoped<WritingAiService>();
// The UI is English, so format dates and numbers the English way whatever the browser language is
// (otherwise month names, decimal separators and thousands separators follow the browser locale).
var english = new System.Globalization.CultureInfo("en-US");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = english;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = english;
System.Globalization.CultureInfo.CurrentCulture = english;
System.Globalization.CultureInfo.CurrentUICulture = english;

await builder.Build().RunAsync();