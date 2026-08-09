using System.Globalization;
using RenewTheDoc.App.Localization;
using RenewTheDoc.Core;

namespace RenewTheDoc.App.Pages;

[QueryProperty(nameof(EditTarget), "edit")]
public partial class AddDocumentPage : ContentPage
{
    private readonly IDocumentStore _store;
    private readonly IOwnerStore _owners;
    private readonly IReminderScheduler _scheduler;
    private readonly List<Button> _segments = [];
    private readonly IReadOnlyList<(string Code, string Name)> _countries;
    private List<Owner> _ownerList = [];
    private Guid? _selectedOwnerId;
    private int _selectedSegment = 1; // default: 1 month
    private Document? _editTarget;

    private static readonly (string Key, RemindBefore? Value)[] RemindOptions =
    [
        ("OneWeek", RemindBefore.OneWeek),
        ("OneMonth", RemindBefore.OneMonth),
        ("ThreeMonths", RemindBefore.ThreeMonths),
        ("CustomDays", null),
    ];

    public Document? EditTarget
    {
        get => _editTarget;
        set { _editTarget = value; ApplyEditTarget(); }
    }

    public AddDocumentPage(IDocumentStore store, IOwnerStore owners, IReminderScheduler scheduler)
    {
        InitializeComponent();
        _store = store;
        _owners = owners;
        _scheduler = scheduler;

        for (var i = 0; i < RemindOptions.Length; i++)
        {
            var index = i;
            var button = new Button { Text = L.T(RemindOptions[i].Key) };
            button.Clicked += (_, _) => SelectSegment(index);
            SegmentGrid.Add(button, i);
            _segments.Add(button);
        }
        SelectSegment(_selectedSegment);

        _countries = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Select(c => { try { return new RegionInfo(c.Name); } catch { return null; } })
            .Where(r => r is { TwoLetterISORegionName.Length: 2 })
            .DistinctBy(r => r!.TwoLetterISORegionName)
            .Select(r => (r!.TwoLetterISORegionName, r.DisplayName))
            .OrderBy(x => x.DisplayName, StringComparer.CurrentCulture)
            .ToList();
        CountryPicker.ItemsSource = new[] { "—" }.Concat(_countries.Select(c => c.Name)).ToList();
        CountryPicker.SelectedIndex = 0;

        ExpiryPicker.Date = DateTime.Now.Date.AddMonths(6);
        _ = LoadOwnersAsync(null);
    }

    /// <summary>Rebuilds the owner picker: Me · dictionary owners · "+ New owner…".</summary>
    private async Task LoadOwnersAsync(Guid? select)
    {
        _ownerList = (await _owners.GetAllAsync()).ToList();
        OwnerPicker.ItemsSource = new[] { L.T("OwnerMe") }
            .Concat(_ownerList.Select(o => o.Name))
            .Concat([L.T("OwnerNew")])
            .ToList();
        var index = select is { } id ? _ownerList.FindIndex(o => o.Id == id) : -1;
        OwnerPicker.SelectedIndex = index >= 0 ? index + 1 : 0;
        _selectedOwnerId = index >= 0 ? select : null;
    }

    private async void OnOwnerChanged(object? sender, EventArgs e)
    {
        var i = OwnerPicker.SelectedIndex;
        if (i < 0) return;

        if (i == _ownerList.Count + 1) // "+ New owner…"
        {
            var name = (await DisplayPromptAsync(L.T("NewOwnerTitle"), L.T("NewOwnerPrompt"),
                L.T("Ok"), L.T("Cancel")))?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                OwnerPicker.SelectedIndex = 0;
                return;
            }
            var owner = new Owner { Name = name };
            await _owners.AddAsync(owner);
            await LoadOwnersAsync(owner.Id);
            return;
        }

        _selectedOwnerId = i == 0 ? null : _ownerList[i - 1].Id;
    }

    private void ApplyEditTarget()
    {
        if (_editTarget is not { } doc) return;

        Title = L.T("EditDocumentTitle");
        DeleteButton.IsVisible = true;
        NameEntry.Text = doc.Name;
        ExpiryPicker.Date = doc.ExpiryDate.ToDateTime(TimeOnly.MinValue);
        NoteEntry.Text = doc.Note;

        var preset = Array.FindIndex(RemindOptions, o => o.Value?.Days == doc.RemindBefore.Days);
        if (preset >= 0)
        {
            SelectSegment(preset);
        }
        else
        {
            SelectSegment(RemindOptions.Length - 1);
            CustomDaysEntry.Text = doc.RemindBefore.Days.ToString();
        }

        var countryIndex = _countries.ToList().FindIndex(c => c.Code == doc.CountryCode);
        CountryPicker.SelectedIndex = countryIndex >= 0 ? countryIndex + 1 : 0;

        _ = LoadOwnersAsync(doc.OwnerId);
    }

    private void SelectSegment(int index)
    {
        _selectedSegment = index;
        for (var i = 0; i < _segments.Count; i++)
            _segments[i].Style = (Style)Application.Current!.Resources[i == index ? "SegmentSelected" : "Segment"];
        CustomDaysBorder.IsVisible = RemindOptions[index].Value is null;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            await DisplayAlertAsync(L.T("ValidationTitle"), L.T("NameRequired"), L.T("Ok"));
            return;
        }

        RemindBefore remindBefore;
        var selected = RemindOptions[_selectedSegment].Value;
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
            Id = _editTarget?.Id ?? Guid.NewGuid(),
            Name = name,
            ExpiryDate = DateOnly.FromDateTime(ExpiryPicker.Date ?? DateTime.Now.Date),
            RemindBefore = remindBefore,
            Note = string.IsNullOrWhiteSpace(NoteEntry.Text) ? null : NoteEntry.Text.Trim(),
            CountryCode = CountryPicker.SelectedIndex > 0 ? _countries[CountryPicker.SelectedIndex - 1].Code : null,
            OwnerId = _selectedOwnerId,
        };

        if (_editTarget is null)
        {
            await _store.AddAsync(document);
        }
        else
        {
            await _store.UpdateAsync(document);
            await _scheduler.CancelAsync(document.Id); // edit = re-creation (CONTEXT.md)
        }
        await _scheduler.ScheduleAsync(document);
        await Shell.Current.GoToAsync("..");
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_editTarget is not { } doc) return;
        var confirmed = await DisplayAlertAsync(
            L.T("DeleteConfirmTitle"), L.F("DeleteConfirmText", doc.Name), L.T("Delete"), L.T("Cancel"));
        if (!confirmed) return;

        await _scheduler.CancelAsync(doc.Id);
        await _store.DeleteAsync(doc.Id);
        await Shell.Current.GoToAsync("..");
    }
}
