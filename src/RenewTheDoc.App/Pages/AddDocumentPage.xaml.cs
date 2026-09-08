using System.Globalization;
using RenewTheDoc.App.Localization;
using RenewTheDoc.Application.Documents;
using RenewTheDoc.Domain;
using RenewTheDoc.Domain.Documents;

namespace RenewTheDoc.App.Pages;

[QueryProperty(nameof(EditTarget), "edit")]
public partial class AddDocumentPage : ContentPage
{
    private readonly DocumentAppService _documents;
    private readonly OwnerAppService _owners;
    private readonly List<Button> _segments = [];
    private readonly IReadOnlyList<(string Code, string Name)> _countries;
    private List<Owner> _ownerList = [];
    private DocumentOwner _selectedOwner = DocumentOwner.Me;
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

    public AddDocumentPage(DocumentAppService documents, OwnerAppService owners)
    {
        InitializeComponent();
        _documents = documents;
        _owners = owners;

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
        _ = LoadOwnersAsync(DocumentOwner.Me);
    }

    /// <summary>
    /// Rebuilds the owner picker: Me · dictionary owners · "+ New owner…". A person the dictionary
    /// no longer knows falls back to Me, as it always has.
    /// </summary>
    private async Task LoadOwnersAsync(DocumentOwner select)
    {
        _ownerList = (await _owners.ListAsync()).ToList();
        OwnerPicker.ItemsSource = new[] { L.T("OwnerMe") }
            .Concat(_ownerList.Select(o => o.Name))
            .Concat([L.T("OwnerNew")])
            .ToList();
        var index = select is DocumentOwner.Person person
            ? _ownerList.FindIndex(o => o.Id == person.Id)
            : -1;
        OwnerPicker.SelectedIndex = index >= 0 ? index + 1 : 0;
        _selectedOwner = index >= 0 ? select : DocumentOwner.Me;
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
            try
            {
                var owner = await _owners.AddAsync(name);
                await LoadOwnersAsync(new DocumentOwner.Person(owner.Id));
            }
            catch (DomainRuleViolationException violation)
            {
                // Owner now enforces its own name rules, so the prompt answers to the same
                // localized alert the save path uses, and the picker falls back to Me.
                await DisplayAlertAsync(
                    L.T("ValidationTitle"), DomainRuleMessages.Localized(violation.Rule), L.T("Ok"));
                OwnerPicker.SelectedIndex = 0;
            }
            return;
        }

        _selectedOwner = i == 0
            ? DocumentOwner.Me
            : new DocumentOwner.Person(_ownerList[i - 1].Id);
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

        var countryIndex = _countries.ToList().FindIndex(c => c.Code == doc.Country?.Code);
        CountryPicker.SelectedIndex = countryIndex >= 0 ? countryIndex + 1 : 0;

        _ = LoadOwnersAsync(doc.Owner);
    }

    private void SelectSegment(int index)
    {
        _selectedSegment = index;
        for (var i = 0; i < _segments.Count; i++)
            _segments[i].Style = (Style)Microsoft.Maui.Controls.Application.Current!.Resources[i == index ? "SegmentSelected" : "Segment"];
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

        var expiryDate = DateOnly.FromDateTime(ExpiryPicker.Date ?? DateTime.Now.Date);
        var note = string.IsNullOrWhiteSpace(NoteEntry.Text) ? null : NoteEntry.Text.Trim();
        var countryCode = CountryPicker.SelectedIndex > 0
            ? _countries[CountryPicker.SelectedIndex - 1].Code
            : null;

        try
        {
            // The aggregate decides what a valid Document is; the page only says which use case it
            // is. Edit keeps the identity, so the reminder is cancelled and re-planned for the same
            // document rather than a new one.
            var country = Country.OfNullable(countryCode);
            // The clock is read here, at the edge, and travels in as an argument — the adapter used
            // to read it while deciding the reminder, which is exactly what stopped (spec §4.1).
            var nowLocal = DateTime.Now;
            if (_editTarget is not { } editTarget)
            {
                await _documents.AddAsync(
                    Document.Create(name, expiryDate, remindBefore, _selectedOwner, note, country),
                    nowLocal);
            }
            else
            {
                await _documents.EditAsync(
                    editTarget.Edit(name, expiryDate, remindBefore, _selectedOwner, note, country),
                    nowLocal);
            }
        }
        catch (DomainRuleViolationException violation)
        {
            // A broken invariant is a form problem — shown with the same alert, in the user's
            // language, via the code → resource-key mapping.
            await DisplayAlertAsync(
                L.T("ValidationTitle"), DomainRuleMessages.Localized(violation.Rule), L.T("Ok"));
            return;
        }
        await Shell.Current.GoToAsync("..");
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_editTarget is not { } doc) return;
        var confirmed = await DisplayAlertAsync(
            L.T("DeleteConfirmTitle"), L.F("DeleteConfirmText", doc.Name), L.T("Delete"), L.T("Cancel"));
        if (!confirmed) return;

        await _documents.DeleteAsync(doc.Id);
        await Shell.Current.GoToAsync("..");
    }
}
