using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using RenewTheDoc.App.Pages;
using RenewTheDoc.App.Services;
using RenewTheDoc.Application.Documents;
using RenewTheDoc.Domain.Documents;
using RenewTheDoc.Persistence;
using RenewTheDoc.Persistence.Documents;

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
        Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) =>
            handler.PlatformView.BackgroundTintList =
                Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
#endif

        // One connection for the app, its tables created here and only here. Blocking is deliberate:
        // MAUI offers no async startup hook and the tables must exist before the first page appears.
        // Two CREATE TABLE statements, and SqliteDatabase.InitializeAsync deliberately does not
        // capture this thread's context — otherwise waiting here deadlocks it (spec §5.4).
        var database = new SqliteDatabase(Path.Combine(FileSystem.AppDataDirectory, "renewthedoc.db3"));
        database.InitializeAsync().GetAwaiter().GetResult();

        builder.Services.AddSingleton(database);
        builder.Services.AddSingleton<IDocumentRepository, SqliteDocumentRepository>();
        builder.Services.AddSingleton<IOwnerRepository, SqliteOwnerRepository>();
        builder.Services.AddSingleton<IReminderScheduler, LocalNotificationReminderScheduler>();
        builder.Services.AddSingleton<DocumentAppService>();
        builder.Services.AddSingleton<OwnerAppService>();
        builder.Services.AddTransient<DocumentListPage>();
        builder.Services.AddTransient<AddDocumentPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
