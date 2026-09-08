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

    private static Document Doc(string name, DateOnly expiry, int remindDays = 30,
        DocumentId? id = null, OwnerId? ownerId = null) =>
        id is { } stored
            ? Document.Restore(stored, name, expiry, new RemindBefore(remindDays), ownerId: ownerId)
            : Document.Create(name, expiry, new RemindBefore(remindDays), ownerId: ownerId);

    [Fact]
    public async Task Add_saves_then_schedules()
    {
        var log = new CallLog();
        var store = new FakeDocumentStore(log);
        var scheduler = new FakeReminderScheduler(log);
        var service = new DocumentAppService(store, scheduler);
        var document = Doc("Passport", new DateOnly(2026, 6, 1));

        await service.AddAsync(document);

        Assert.Equal(["documents.Add(Passport)", "scheduler.Schedule(Passport)"], log.Calls);
        Assert.Same(document, Assert.Single(store.Added));
        Assert.Same(document, Assert.Single(scheduler.Scheduled));
        Assert.Empty(scheduler.Cancelled);
    }

    /// <summary>
    /// Today's edit path updates, *then* cancels, then re-schedules. Reads backwards; works because
    /// the adapter's cancel and schedule are independent of the write. Pinned as-is — the reorder
    /// belongs to a later step, not to the extraction.
    /// </summary>
    [Fact]
    public async Task Edit_updates_then_cancels_then_reschedules()
    {
        var log = new CallLog();
        var id = DocumentId.New();
        var store = new FakeDocumentStore(log);
        var scheduler = new FakeReminderScheduler(log);
        var service = new DocumentAppService(store, scheduler);
        var document = Doc("Passport", new DateOnly(2026, 6, 1), id: id);

        await service.EditAsync(document);

        Assert.Equal(
            ["documents.Update(Passport)", $"scheduler.Cancel({id})", "scheduler.Schedule(Passport)"],
            log.Calls);
        Assert.Same(document, Assert.Single(store.Updated));
        Assert.Equal(id, Assert.Single(scheduler.Cancelled));
        Assert.Same(document, Assert.Single(scheduler.Scheduled));
        Assert.Empty(store.Added);
    }

    [Fact]
    public async Task Delete_cancels_then_deletes()
    {
        var log = new CallLog();
        var id = DocumentId.New();
        var store = new FakeDocumentStore(log);
        var scheduler = new FakeReminderScheduler(log);
        var service = new DocumentAppService(store, scheduler);

        await service.DeleteAsync(id);

        Assert.Equal([$"scheduler.Cancel({id})", $"documents.Delete({id})"], log.Calls);
        Assert.Equal(id, Assert.Single(scheduler.Cancelled));
        Assert.Equal(id, Assert.Single(store.Deleted));
        Assert.Empty(scheduler.Scheduled);
    }

    [Fact]
    public async Task Ensure_permission_asks_the_scheduler_and_nothing_else()
    {
        var log = new CallLog();
        var store = new FakeDocumentStore(log);
        var scheduler = new FakeReminderScheduler(log);
        var service = new DocumentAppService(store, scheduler);

        await service.EnsureNotificationPermissionAsync();

        Assert.Equal(["scheduler.EnsurePermission"], log.Calls);
    }

    [Fact]
    public async Task List_reads_documents_once_and_touches_no_other_collaborator()
    {
        var log = new CallLog();
        var store = new FakeDocumentStore(log, Doc("Passport", new DateOnly(2026, 6, 1)));
        var scheduler = new FakeReminderScheduler(log);
        var service = new DocumentAppService(store, scheduler);

        await service.ListAsync(ownerFilterActive: false, ownerId: null, statusFilter: null, Today);

        Assert.Equal(["documents.GetAll"], log.Calls);
    }

    [Fact]
    public async Task List_groups_by_state_in_glossary_order_expired_first()
    {
        var ok = Doc("Ok", new DateOnly(2027, 1, 1));
        var soon = Doc("Soon", new DateOnly(2026, 1, 20));
        var expired = Doc("Expired", new DateOnly(2025, 12, 1));
        var service = Service(ok, soon, expired);

        var groups = await service.ListAsync(false, null, null, Today);

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

        var groups = await service.ListAsync(false, null, null, Today);

        Assert.Equal(["Sooner", "Later"], Assert.Single(groups).Documents.Select(d => d.Name));
    }

    [Fact]
    public async Task List_with_no_owner_filter_returns_everyones_documents()
    {
        var ownerId = OwnerId.New();
        var service = Service(
            Doc("Mine", new DateOnly(2027, 1, 1)),
            Doc("Theirs", new DateOnly(2027, 2, 1), ownerId: ownerId));

        var groups = await service.ListAsync(ownerFilterActive: false, ownerId: null, null, Today);

        Assert.Equal(["Mine", "Theirs"], Assert.Single(groups).Documents.Select(d => d.Name));
    }

    [Fact]
    public async Task List_filtered_to_Me_returns_only_documents_without_an_owner()
    {
        var ownerId = OwnerId.New();
        var service = Service(
            Doc("Mine", new DateOnly(2027, 1, 1)),
            Doc("Theirs", new DateOnly(2027, 2, 1), ownerId: ownerId));

        var groups = await service.ListAsync(ownerFilterActive: true, ownerId: null, null, Today);

        Assert.Equal(["Mine"], Assert.Single(groups).Documents.Select(d => d.Name));
    }

    [Fact]
    public async Task List_filtered_to_an_owner_returns_only_their_documents()
    {
        var ownerId = OwnerId.New();
        var service = Service(
            Doc("Mine", new DateOnly(2027, 1, 1)),
            Doc("Theirs", new DateOnly(2027, 2, 1), ownerId: ownerId));

        var groups = await service.ListAsync(ownerFilterActive: true, ownerId, null, Today);

        Assert.Equal(["Theirs"], Assert.Single(groups).Documents.Select(d => d.Name));
    }

    [Fact]
    public async Task List_filtered_by_status_keeps_only_that_group()
    {
        var service = Service(
            Doc("Ok", new DateOnly(2027, 1, 1)),
            Doc("Soon", new DateOnly(2026, 1, 20)),
            Doc("Expired", new DateOnly(2025, 12, 1)));

        var groups = await service.ListAsync(false, null, DocumentState.ExpiringSoon, Today);

        var group = Assert.Single(groups);
        Assert.Equal(DocumentState.ExpiringSoon, group.State);
        Assert.Equal(["Soon"], group.Documents.Select(d => d.Name));
    }

    [Fact]
    public async Task List_of_nothing_is_no_groups()
    {
        var groups = await Service().ListAsync(false, null, null, Today);

        Assert.Empty(groups);
    }

    private static DocumentAppService Service(params Document[] seed)
    {
        var log = new CallLog();
        return new DocumentAppService(new FakeDocumentStore(log, seed), new FakeReminderScheduler(log));
    }
}
