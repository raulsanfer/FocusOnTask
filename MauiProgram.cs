using Microsoft.Extensions.Logging;

using FocusOnTask.Data;
using FocusOnTask.Services;
using Microsoft.EntityFrameworkCore;

namespace FocusOnTask;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "focusontask.db");

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddDbContextFactory<FocusOnTaskDbContext>(options =>
        {
            options.UseSqlite($"Data Source={databasePath}");
        });
        builder.Services.AddSingleton<FocusWorkspaceService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
