using RenewTheDoc.App.Localization;
using RenewTheDoc.App.Services;
using RenewTheDoc.Core;

namespace RenewTheDoc.App.Pages;

public partial class DocumentListPage : ContentPage
{
    private readonly IDocumentStore _store;

    public DocumentListPage(IDocumentStore store)
    {
        InitializeComponent();
        _store = store;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LocalNotificationReminderScheduler.EnsurePermissionAsync();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var documents = DocumentListOrder.Sorted(await _store.GetAllAsync(), today);
        DocumentsView.ItemsSource = documents.Select(d => DocumentListItem.From(d, today)).ToList();
    }

    private async void OnAddClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(AddDocumentPage));
}

public sealed record DocumentListItem(string Name, string ExpiresText, string StateText, Color StateColor)
{
    public static DocumentListItem From(Document d, DateOnly today)
    {
        var state = d.GetState(today);
        return new DocumentListItem(
            d.Name,
            L.F("ExpiresOn", d.ExpiryDate.ToString("d")),
            L.T(state switch
            {
                DocumentState.Expired => "StateExpired",
                DocumentState.ExpiringSoon => "StateExpiringSoon",
                _ => "StateOk",
            }),
            state switch
            {
                DocumentState.Expired => Color.FromArgb("#D64545"),
                DocumentState.ExpiringSoon => Color.FromArgb("#E8A33D"),
                _ => Color.FromArgb("#3D9A5F"),
            });
    }
}
