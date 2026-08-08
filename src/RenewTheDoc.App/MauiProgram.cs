using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using RenewTheDoc.App.Pages;
using RenewTheDoc.App.Services;
using RenewTheDoc.Core;

namespace RenewTheDoc.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseLocalNotification()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<IDocumentStore>(
            new SqliteDocumentStore(Path.Combine(FileSystem.AppDataDirectory, "renewthedoc.db3")));
        builder.Services.AddSingleton<IReminderScheduler, LocalNotificationReminderScheduler>();
        builder.Services.AddTransient<DocumentListPage>();
        builder.Services.AddTransient<AddDocumentPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
