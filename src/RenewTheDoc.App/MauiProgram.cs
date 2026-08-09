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
                fonts.AddFont("Manrope-400.ttf", "ManropeRegular");
                fonts.AddFont("Manrope-600.ttf", "ManropeSemiBold");
                fonts.AddFont("Manrope-800.ttf", "ManropeExtraBold");
            });

#if ANDROID
        // Compass fields draw their own surface; kill the native EditText underline.
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) =>
            handler.PlatformView.BackgroundTintList =
                Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
        Microsoft.Maui.Handlers.DatePickerHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) =>
            handler.PlatformView.BackgroundTintList =
                Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
#endif

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
