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

        var groups = documents
            .GroupBy(d => d.GetState(today))
            .OrderBy(g => g.Key)
            .Select(g => new DocumentGroup(
                L.T(g.Key switch
                {
                    DocumentState.Expired => "GroupNeedsAttention",
                    DocumentState.ExpiringSoon => "GroupComingUp",
                    _ => "GroupAllGood",
                }),
                g.Select(d => DocumentListItem.From(d, today))))
            .ToList();

        DocumentsView.ItemsSource = groups;
    }

    private async void OnAddClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(AddDocumentPage));
}

public sealed class DocumentGroup : List<DocumentListItem>
{
    public string Title { get; }
    public DocumentGroup(string title, IEnumerable<DocumentListItem> items) : base(items) => Title = title;
}

public sealed record DocumentListItem(string Name, string DateText, string NumberText, string UnitText, Color StateColor)
{
    public static DocumentListItem From(Document d, DateOnly today)
    {
        var state = d.GetState(today);
        var days = d.ExpiryDate.DayNumber - today.DayNumber;

        var (number, unit) = state == DocumentState.Expired
            ? (Math.Abs(days).ToString(), L.T("UnitDaysAgo"))
            : days > 365
                ? ((days / 365.25).ToString("0.#"), L.T("UnitYears"))
                : (days.ToString(), L.T("UnitDays"));

        var dateText = state == DocumentState.Expired
            ? L.F("ExpiredOn", d.ExpiryDate.ToString("d"))
            : L.F("ExpiresOn", d.ExpiryDate.ToString("d"));

        var color = Application.Current!.RequestedTheme == AppTheme.Dark
            ? state switch
            {
                DocumentState.Expired => Color.FromArgb("#E07A6C"),
                DocumentState.ExpiringSoon => Color.FromArgb("#E0A24A"),
                _ => Color.FromArgb("#5CBB8C"),
            }
            : state switch
            {
                DocumentState.Expired => Color.FromArgb("#C4473B"),
                DocumentState.ExpiringSoon => Color.FromArgb("#C77D1D"),
                _ => Color.FromArgb("#31855C"),
            };

        return new DocumentListItem(d.Name, dateText, number, unit, color);
    }
}
