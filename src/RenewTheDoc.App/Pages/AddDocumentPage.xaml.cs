using RenewTheDoc.App.Localization;
using RenewTheDoc.Core;

namespace RenewTheDoc.App.Pages;

public partial class AddDocumentPage : ContentPage
{
    private readonly IDocumentStore _store;
    private readonly IReminderScheduler _scheduler;

    private static readonly (string Key, RemindBefore? Value)[] RemindOptions =
    [
        ("OneWeek", RemindBefore.OneWeek),
        ("OneMonth", RemindBefore.OneMonth),
        ("ThreeMonths", RemindBefore.ThreeMonths),
        ("CustomDays", null),
    ];

    public AddDocumentPage(IDocumentStore store, IReminderScheduler scheduler)
    {
        InitializeComponent();
        _store = store;
        _scheduler = scheduler;

        RemindPicker.ItemsSource = RemindOptions.Select(o => L.T(o.Key)).ToList();
        RemindPicker.SelectedIndex = 1; // default: 1 month
        ExpiryPicker.Date = DateTime.Now.Date.AddMonths(6);
    }

    private void OnRemindOptionChanged(object? sender, EventArgs e) =>
        CustomDaysEntry.IsVisible = RemindOptions[RemindPicker.SelectedIndex].Value is null;

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            await DisplayAlertAsync(L.T("ValidationTitle"), L.T("NameRequired"), L.T("Ok"));
            return;
        }

        RemindBefore remindBefore;
        var selected = RemindOptions[RemindPicker.SelectedIndex].Value;
        if (selected is { } preset)
        {
            remindBefore = preset;
        }
        else if (int.TryParse(CustomDaysEntry.Text, out var days) && days >= 0)
        {
            remindBefore = new RemindBefore(days);
        }
        else
        {
            await DisplayAlertAsync(L.T("ValidationTitle"), L.T("InvalidCustomDays"), L.T("Ok"));
            return;
        }

        var document = new Document
        {
            Name = name,
            ExpiryDate = DateOnly.FromDateTime(ExpiryPicker.Date ?? DateTime.Now.Date),
            RemindBefore = remindBefore,
            Note = string.IsNullOrWhiteSpace(NoteEntry.Text) ? null : NoteEntry.Text.Trim(),
        };

        await _store.AddAsync(document);
        await _scheduler.ScheduleAsync(document);
        await Shell.Current.GoToAsync("..");
    }
}
