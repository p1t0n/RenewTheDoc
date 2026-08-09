using RenewTheDoc.App.Localization;
using RenewTheDoc.App.Services;
using RenewTheDoc.Core;

namespace RenewTheDoc.App.Pages;

public partial class DocumentListPage : ContentPage
{
    private readonly IDocumentStore _store;
    private readonly IOwnerStore _owners;
    private readonly IReminderScheduler _scheduler;

    public DocumentListPage(IDocumentStore store, IOwnerStore owners, IReminderScheduler scheduler)
    {
        InitializeComponent();
        _store = store;
        _owners = owners;
        _scheduler = scheduler;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LocalNotificationReminderScheduler.EnsurePermissionAsync();
        await RefreshAsync();
    }

    // Filter state (page-level, not persisted). _ownerFilterActive false = "All";
    // when active, _ownerFilter null means "Me" (documents without an owner).
    private bool _ownerFilterActive;
    private Guid? _ownerFilter;
    private DocumentState? _statusFilter;

    private async Task RefreshAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var owners = await _owners.GetAllAsync();
        var ownerNames = owners.ToDictionary(o => o.Id, o => o.Name);

        var documents = DocumentListOrder.Sorted(await _store.GetAllAsync(), today).AsEnumerable();
        if (_ownerFilterActive)
            documents = documents.Where(d => d.OwnerId == _ownerFilter);
        if (_statusFilter is { } state)
            documents = documents.Where(d => d.GetState(today) == state);

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
                g.Select(d => DocumentListItem.From(d, today,
                    !_ownerFilterActive && d.OwnerId is { } oid ? ownerNames.GetValueOrDefault(oid) : null))))
            .ToList();

        DocumentsView.ItemsSource = groups;
        BuildChips(owners);
    }

    private void BuildChips(IReadOnlyList<Owner> owners)
    {
        OwnerChips.Clear();
        OwnerChips.Add(Chip(L.T("FilterAll"), !_ownerFilterActive, null,
            () => { _ownerFilterActive = false; _ownerFilter = null; _ = RefreshAsync(); }));
        OwnerChips.Add(Chip(L.T("OwnerMe"), _ownerFilterActive && _ownerFilter is null, null,
            () => { _ownerFilterActive = true; _ownerFilter = null; _ = RefreshAsync(); }));
        foreach (var owner in owners)
            OwnerChips.Add(Chip(owner.Name, _ownerFilterActive && _ownerFilter == owner.Id, null,
                () => { _ownerFilterActive = true; _ownerFilter = owner.Id; _ = RefreshAsync(); }));

        StatusChips.Clear();
        StatusChips.Add(Chip(L.T("FilterAll"), _statusFilter is null, null,
            () => { _statusFilter = null; _ = RefreshAsync(); }));
        foreach (var (state, key) in new[]
        {
            (DocumentState.Expired, "GroupNeedsAttention"),
            (DocumentState.ExpiringSoon, "GroupComingUp"),
            (DocumentState.Ok, "GroupAllGood"),
        })
            StatusChips.Add(Chip(L.T(key), _statusFilter == state, StateColor(state),
                () => { _statusFilter = state; _ = RefreshAsync(); }));
    }

    private Border Chip(string text, bool selected, Color? dot, Action onTap)
    {
        var content = new HorizontalStackLayout { Spacing = 5, VerticalOptions = LayoutOptions.Center };
        if (dot is not null)
            content.Add(new BoxView { Color = dot, WidthRequest = 7, HeightRequest = 7, CornerRadius = 3.5f, VerticalOptions = LayoutOptions.Center });
        var label = new Label
        {
            Text = text,
            FontSize = 12,
            FontFamily = selected ? "ManropeExtraBold" : "ManropeSemiBold",
            VerticalOptions = LayoutOptions.Center,
        };
        content.Add(label);

        var chip = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 999 },
            StrokeThickness = selected ? 0 : 1,
            Padding = new Thickness(13, 6),
            Content = content,
        };
        if (selected)
        {
            chip.SetAppThemeColor(BackgroundColorProperty, Tok("PrimaryL"), Tok("PrimaryD"));
            label.SetAppThemeColor(Label.TextColorProperty, Tok("OnPrimaryL"), Tok("OnPrimaryD"));
        }
        else
        {
            chip.SetAppThemeColor(BackgroundColorProperty, Tok("SurfaceL"), Tok("SurfaceD"));
            chip.SetAppThemeColor(Border.StrokeProperty, Tok("LineL"), Tok("LineD"));
            label.SetAppThemeColor(Label.TextColorProperty, Tok("MutedL"), Tok("MutedD"));
        }
        chip.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(onTap) });
        return chip;
    }

    private static Color Tok(string key) => (Color)Application.Current!.Resources[key];

    private static Color StateColor(DocumentState state) =>
        Application.Current!.RequestedTheme == AppTheme.Dark
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

    private async void OnAddClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(AddDocumentPage));

    private async void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not DocumentListItem item) return;
        await Shell.Current.GoToAsync(nameof(AddDocumentPage),
            new Dictionary<string, object> { ["edit"] = item.Source });
    }

    private async void OnSwipeDelete(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not DocumentListItem item) return;
        var confirmed = await DisplayAlertAsync(
            L.T("DeleteConfirmTitle"), L.F("DeleteConfirmText", item.Name), L.T("Delete"), L.T("Cancel"));
        if (!confirmed) return;

        await _scheduler.CancelAsync(item.Source.Id);
        await _store.DeleteAsync(item.Source.Id);
        await RefreshAsync();
    }
}

public sealed class DocumentGroup : List<DocumentListItem>
{
    public string Title { get; }
    public DocumentGroup(string title, IEnumerable<DocumentListItem> items) : base(items) => Title = title;
}

public sealed record DocumentListItem(Document Source, string Name, string DateText, string NumberText, string UnitText, Color StateColor)
{
    public static DocumentListItem From(Document d, DateOnly today, string? ownerName = null)
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
        if (d.CountryCode is { } cc) dateText += $" · {cc}";
        if (ownerName is not null) dateText = $"{ownerName} · {dateText}";

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

        return new DocumentListItem(d, d.Name, dateText, number, unit, color);
    }
}
