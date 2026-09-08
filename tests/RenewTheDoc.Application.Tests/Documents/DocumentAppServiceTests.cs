using RenewTheDoc.Application.Documents;
using RenewTheDoc.Domain.Documents;

namespace RenewTheDoc.Application.Tests.Documents;

/// <summary>
/// Characterization tests: they pin the call sequences the pages performed before the extraction,
/// quirks included. If one of these fails, behaviour moved — which no structural step may do.
/// </summary>
public class DocumentAppServiceTests
{
    private static readonly DateOnly Today = new(2026, 1, 1);
    private static readonly DateTime NowLocal = new(2026, 1, 1, 8, 0, 0);

    private static Document Doc(string name, DateOnly expiry, int remindDays = 30,
        DocumentId? id = null, DocumentOwner? owner = null)
    {
        var documentOwner = owner ?? DocumentOwner.Me;
        return id is { } stored
            ? Document.Restore(stored, name, expiry, new RemindBefore(remindDays), documentOwner)
            : Document.Create(name, expiry, new RemindBefore(remindDays), documentOwner);
    }

    [Fact]
    public async Task Add_saves_then_schedules()
    {
        var log = new CallLog();
        var repository = new FakeDocumentRepository(log);
        var scheduler = new FakeReminderScheduler(log);
        var service = new DocumentAppService(repository, scheduler);
        var document = Doc("Passport", new DateOnly(2026, 6, 1));

        await service.AddAsync(document, NowLocal);

        Assert.Equal(["documents.Save(Passport)", "scheduler.Schedule(Passport)"], log.Calls);
        Assert.Same(document, Assert.Single(repository.Saved));
        var scheduled = Assert.Single(scheduler.Scheduled);
        Assert.Equal(document.Id, scheduled.Id);
        Assert.Equal(new ReminderContent("Passport", new DateOnly(2026, 6, 1)), scheduled.Content);
        Assert.Empty(scheduler.Cancelled);
    }

    /// <summary>
    /// Today's edit path writes, *then* cancels, then re-schedules. Reads backwards; works because
    /// the adapter's cancel and schedule are independent of the write. Pinned as-is — the reorder
    /// belongs to a later step, not to the extraction.
    /// </summary>
    [Fact]
    public async Task Edit_writes_then_cancels_then_reschedules()
    {
        var log = new CallLog();
        var id = DocumentId.New();
        var repository = new FakeDocumentRepository(log);
        var scheduler = new FakeReminderScheduler(log);
        var service = new DocumentAppService(repository, scheduler);
        var document = Doc("Passport", new DateOnly(2026, 6, 1), id: id);

        await service.EditAsync(document, NowLocal);

        Assert.Equal(
            ["documents.Save(Passport)", $"scheduler.Cancel({id})", "scheduler.Schedule(Passport)"],
            log.Calls);
        Assert.Same(document, Assert.Single(repository.Saved));
        Assert.Equal(id, Assert.Single(scheduler.Cancelled));
        Assert.Equal(id, Assert.Single(scheduler.Scheduled).Id);
        Assert.Empty(repository.Removed);
    }

    /// <summary>
    /// CONTEXT.md's "editing behaves like re-creation" rule, now assertable with a fake scheduler
    /// and no platform present: the same identity is cancelled and then re-planned from the edited
    /// state, so the new expiry — not the old one — decides the moment.
    /// </summary>
    [Fact]
    public async Task Edit_cancels_the_old_reminder_and_re_plans_from_the_edited_state()
    {
        var log = new CallLog();
        var scheduler = new FakeReminderScheduler(log);
        var service = new DocumentAppService(new FakeDocumentRepository(log), scheduler);
        var stored = Doc("Passport", new DateOnly(2026, 6, 1), remindDays: 30, id: DocumentId.New());

        var edited = stored.Edit(
            "Passport", new DateOnly(2026, 3, 1), new RemindBefore(7), DocumentOwner.Me);
        await service.EditAsync(edited, NowLocal);

        Assert.Equal(stored.Id, Assert.Single(scheduler.Cancelled));
        var scheduled = Assert.Single(scheduler.Scheduled);
        Assert.Equal(stored.Id, scheduled.Id);
        Assert.Equal(
            new ReminderInstruction.At(new DateTime(2026, 2, 22, 9, 0, 0)), scheduled.Instruction);
        Assert.Equal(new ReminderContent("Passport", new DateOnly(2026, 3, 1)), scheduled.Content);
    }

    /// <summary>
    /// The instruction crossing the port is the aggregate's, decided against the time the caller
    /// passed in — the adapter reads no clock and plans nothing (spec §4.1).
    /// </summary>
    [Fact]
    public async Task Add_hands_the_scheduler_the_instruction_the_aggregate_planned()
    {
        var log = new CallLog();
        var scheduler = new FakeReminderScheduler(log);
        var service = new DocumentAppService(new FakeDocumentRepository(log), scheduler);
        var document = Doc("Passport", new DateOnly(2026, 6, 1), remindDays: 30);

        await service.AddAsync(document, NowLocal);

        Assert.Equal(
            document.PlanReminder(NowLocal), Assert.Single(scheduler.Scheduled).Instruction);
    }

    /// <summary>An already-past reminder moment still fires once, immediately, as it always has.</summary>
    [Fact]
    public async Task Add_of_a_document_whose_reminder_moment_has_passed_fires_immediately()
    {
        var log = new CallLog();
        var scheduler = new FakeReminderScheduler(log);
        var service = new DocumentAppService(new FakeDocumentRepository(log), scheduler);
        var document = Doc("Passport", new DateOnly(2026, 1, 10), remindDays: 30);

        await service.AddAsync(document, NowLocal);

        Assert.Equal(new ReminderInstruction.Immediate(), Assert.Single(scheduler.Scheduled).Instruction);
    }

    /// <summary>
    /// An expired document gets <c>None</c>. The port is still called — the instruction is the
    /// decision, and skipping the call would put that decision back in the app service.
    /// </summary>
    [Fact]
    public async Task Add_of_an_expired_document_schedules_nothing()
    {
        var log = new CallLog();
        var scheduler = new FakeReminderScheduler(log);
        var service = new DocumentAppService(new FakeDocumentRepository(log), scheduler);
        var document = Doc("Passport", new DateOnly(2025, 12, 1));

        await service.AddAsync(document, NowLocal);

        Assert.Equal(new ReminderInstruction.None(), Assert.Single(scheduler.Scheduled).Instruction);
    }

    [Fact]
    public async Task Delete_cancels_then_deletes()
    {
        var log = new CallLog();
        var id = DocumentId.New();
        var repository = new FakeDocumentRepository(log);
        var scheduler = new FakeReminderScheduler(log);
        var service = new DocumentAppService(repository, scheduler);

        await service.DeleteAsync(id);

        Assert.Equal([$"scheduler.Cancel({id})", $"documents.Remove({id})"], log.Calls);
        Assert.Equal(id, Assert.Single(scheduler.Cancelled));
        Assert.Equal(id, Assert.Single(repository.Removed));
        Assert.Empty(scheduler.Scheduled);
    }

    [Fact]
    public async Task Ensure_permission_asks_the_scheduler_and_nothing_else()
    {
        var log = new CallLog();
        var repository = new FakeDocumentRepository(log);
        var scheduler = new FakeReminderScheduler(log);
        var service = new DocumentAppService(repository, scheduler);

        var granted = await service.EnsureNotificationPermissionAsync();

        Assert.Equal(["scheduler.EnsurePermission"], log.Calls);
        Assert.True(granted);
    }

    /// <summary>
    /// Permission is a port member now, so a refusal is an answer the caller can see rather than
    /// something only the concrete adapter knows.
    /// </summary>
    [Fact]
    public async Task Ensure_permission_reports_a_refusal()
    {
        var log = new CallLog();
        var service = new DocumentAppService(
            new FakeDocumentRepository(log), new FakeReminderScheduler(log, permissionGranted: false));

        Assert.False(await service.EnsureNotificationPermissionAsync());
    }

    [Fact]
    public async Task List_reads_documents_once_and_touches_no_other_collaborator()
    {
        var log = new CallLog();
        var repository = new FakeDocumentRepository(log, Doc("Passport", new DateOnly(2026, 6, 1)));
        var scheduler = new FakeReminderScheduler(log);
        var service = new DocumentAppService(repository, scheduler);

        await service.ListAsync(ownerFilter: null, statusFilter: null, Today);

        Assert.Equal(["documents.GetAll"], log.Calls);
    }

    [Fact]
    public async Task List_groups_by_state_in_glossary_order_expired_first()
    {
        var ok = Doc("Ok", new DateOnly(2027, 1, 1));
        var soon = Doc("Soon", new DateOnly(2026, 1, 20));
        var expired = Doc("Expired", new DateOnly(2025, 12, 1));
        var service = Service(ok, soon, expired);

        var groups = await service.ListAsync(null, null, Today);

        Assert.Equal(
            [DocumentState.Expired, DocumentState.ExpiringSoon, DocumentState.Ok],
            groups.Select(g => g.State));
        Assert.Equal(["Expired"], groups[0].Documents.Select(d => d.Name));
        Assert.Equal(["Soon"], groups[1].Documents.Select(d => d.Name));
        Assert.Equal(["Ok"], groups[2].Documents.Select(d => d.Name));
    }

    [Fact]
    public async Task List_orders_within_a_group_by_nearest_expiry()
    {
        var later = Doc("Later", new DateOnly(2027, 6, 1));
        var sooner = Doc("Sooner", new DateOnly(2027, 1, 1));
        var service = Service(later, sooner);

        var groups = await service.ListAsync(null, null, Today);

        Assert.Equal(["Sooner", "Later"], Assert.Single(groups).Documents.Select(d => d.Name));
    }

    [Fact]
    public async Task List_with_no_owner_filter_returns_everyones_documents()
    {
        var them = new DocumentOwner.Person(OwnerId.New());
        var service = Service(
            Doc("Mine", new DateOnly(2027, 1, 1)),
            Doc("Theirs", new DateOnly(2027, 2, 1), owner: them));

        var groups = await service.ListAsync(ownerFilter: null, null, Today);

        Assert.Equal(["Mine", "Theirs"], Assert.Single(groups).Documents.Select(d => d.Name));
    }

    [Fact]
    public async Task List_filtered_to_Me_returns_only_the_users_own_documents()
    {
        var them = new DocumentOwner.Person(OwnerId.New());
        var service = Service(
            Doc("Mine", new DateOnly(2027, 1, 1)),
            Doc("Theirs", new DateOnly(2027, 2, 1), owner: them));

        var groups = await service.ListAsync(DocumentOwner.Me, null, Today);

        Assert.Equal(["Mine"], Assert.Single(groups).Documents.Select(d => d.Name));
    }

    [Fact]
    public async Task List_filtered_to_an_owner_returns_only_their_documents()
    {
        var them = new DocumentOwner.Person(OwnerId.New());
        var service = Service(
            Doc("Mine", new DateOnly(2027, 1, 1)),
            Doc("Theirs", new DateOnly(2027, 2, 1), owner: them));

        var groups = await service.ListAsync(them, null, Today);

        Assert.Equal(["Theirs"], Assert.Single(groups).Documents.Select(d => d.Name));
    }

    /// <summary>
    /// The filter compares owners by value, so a chip built from a fresh Person instance still
    /// matches the stored one — the list page rebuilds its chips on every refresh.
    /// </summary>
    [Fact]
    public async Task List_filtered_to_an_owner_matches_by_value_not_by_instance()
    {
        var ownerId = OwnerId.New();
        var service = Service(
            Doc("Theirs", new DateOnly(2027, 2, 1), owner: new DocumentOwner.Person(ownerId)));

        var groups = await service.ListAsync(new DocumentOwner.Person(ownerId), null, Today);

        Assert.Equal(["Theirs"], Assert.Single(groups).Documents.Select(d => d.Name));
    }

    [Fact]
    public async Task List_filtered_to_a_different_owner_returns_nothing()
    {
        var service = Service(
            Doc("Mine", new DateOnly(2027, 1, 1)),
            Doc("Theirs", new DateOnly(2027, 2, 1), owner: new DocumentOwner.Person(OwnerId.New())));

        var groups = await service.ListAsync(new DocumentOwner.Person(OwnerId.New()), null, Today);

        Assert.Empty(groups);
    }

    [Fact]
    public async Task List_filtered_by_status_keeps_only_that_group()
    {
        var service = Service(
            Doc("Ok", new DateOnly(2027, 1, 1)),
            Doc("Soon", new DateOnly(2026, 1, 20)),
            Doc("Expired", new DateOnly(2025, 12, 1)));

        var groups = await service.ListAsync(null, DocumentState.ExpiringSoon, Today);

        var group = Assert.Single(groups);
        Assert.Equal(DocumentState.ExpiringSoon, group.State);
        Assert.Equal(["Soon"], group.Documents.Select(d => d.Name));
    }

    [Fact]
    public async Task List_of_nothing_is_no_groups()
    {
        var groups = await Service().ListAsync(null, null, Today);

        Assert.Empty(groups);
    }

    private static DocumentAppService Service(params Document[] seed)
    {
        var log = new CallLog();
        return new DocumentAppService(new FakeDocumentRepository(log, seed), new FakeReminderScheduler(log));
    }
}
