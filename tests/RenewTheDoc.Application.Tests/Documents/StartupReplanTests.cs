using RenewTheDoc.Application.Documents;
using RenewTheDoc.Domain.Documents;

namespace RenewTheDoc.Application.Tests.Documents;

/// <summary>
/// The refactor's one granted behaviour change (spec §8 step 9): on launch every Document's Reminder
/// is planned again from its current data, repairing losses nothing could catch when they happened.
/// The fake scheduler models the platform's pending queue, so these assert what would still be
/// standing after the pass, not merely which calls it made.
/// </summary>
public class StartupReplanTests
{
    private static readonly DateTime NowLocal = new(2026, 1, 1, 8, 0, 0);

    private static Document Doc(string name, DateOnly expiry, int remindDays = 30) =>
        Document.Restore(DocumentId.New(), name, expiry, new RemindBefore(remindDays), DocumentOwner.Me);

    [Fact]
    public async Task A_document_whose_scheduling_failed_ends_up_scheduled()
    {
        var log = new CallLog();
        var passport = Doc("Passport", new DateOnly(2026, 6, 1));
        var scheduler = new FakeReminderScheduler(log);
        var service = new DocumentAppService(new FakeDocumentRepository(log, passport), scheduler);

        // Nothing pending: the write went through on some earlier run and the scheduling did not.
        Assert.Empty(scheduler.Pending);

        await service.ReplanAllRemindersAsync(NowLocal);

        Assert.Equal(
            new ReminderInstruction.At(new DateTime(2026, 5, 2, 9, 0, 0)),
            scheduler.Pending[passport.Id]);
    }

    [Fact]
    public async Task Running_it_twice_leaves_the_same_scheduled_set()
    {
        var log = new CallLog();
        var scheduler = new FakeReminderScheduler(log);
        var service = new DocumentAppService(
            new FakeDocumentRepository(
                log,
                Doc("Passport", new DateOnly(2026, 6, 1)),
                Doc("Insurance", new DateOnly(2026, 1, 15), remindDays: 30), // moment already passed
                Doc("Old ID", new DateOnly(2025, 12, 1))),                   // expired
            scheduler);

        await service.ReplanAllRemindersAsync(NowLocal);
        var afterFirstPass = new Dictionary<DocumentId, ReminderInstruction>(scheduler.Pending);

        await service.ReplanAllRemindersAsync(NowLocal);

        Assert.Equal(afterFirstPass, scheduler.Pending);
        Assert.Equal(2, scheduler.Pending.Count); // the expired one is not in there either time
    }

    [Fact]
    public async Task An_expired_document_is_not_scheduled_and_its_stale_reminder_goes()
    {
        var log = new CallLog();
        var expired = Doc("Old ID", new DateOnly(2025, 12, 1));
        var scheduler = new FakeReminderScheduler(log);
        var service = new DocumentAppService(new FakeDocumentRepository(log, expired), scheduler);

        // A reminder left over from before it expired — the cancel in the pass is what clears it.
        scheduler.Pending[expired.Id] = new ReminderInstruction.At(new DateTime(2025, 11, 1, 9, 0, 0));

        await service.ReplanAllRemindersAsync(NowLocal);

        Assert.Empty(scheduler.Pending);
        Assert.Equal(new ReminderInstruction.None(), Assert.Single(scheduler.Scheduled).Instruction);
    }

    /// <summary>
    /// Reminder moment behind us, expiry ahead: the Reminder still fires, once, exactly as it does
    /// when the same document is added or edited today.
    /// </summary>
    [Fact]
    public async Task A_past_but_not_expired_moment_fires_once()
    {
        var log = new CallLog();
        var soon = Doc("Insurance", new DateOnly(2026, 1, 15), remindDays: 30);
        var scheduler = new FakeReminderScheduler(log);
        var service = new DocumentAppService(new FakeDocumentRepository(log, soon), scheduler);

        await service.ReplanAllRemindersAsync(NowLocal);

        Assert.Equal(new ReminderInstruction.Immediate(), scheduler.Pending[soon.Id]);
        Assert.Single(scheduler.Scheduled);
    }

    [Fact]
    public async Task Every_document_is_cancelled_before_it_is_re_planned()
    {
        var log = new CallLog();
        var passport = Doc("Passport", new DateOnly(2026, 6, 1));
        var scheduler = new FakeReminderScheduler(log);
        var service = new DocumentAppService(new FakeDocumentRepository(log, passport), scheduler);

        await service.ReplanAllRemindersAsync(NowLocal);

        Assert.Equal(
            ["documents.GetAll", $"scheduler.Cancel({passport.Id})", "scheduler.Schedule(Passport)"],
            log.Calls);
    }

    /// <summary>
    /// The pass exists because scheduling fails silently; it must not itself fail loudly. One
    /// Document's refusal costs that Document only, and startup never sees the exception.
    /// </summary>
    [Fact]
    public async Task A_scheduling_failure_stops_neither_the_pass_nor_startup()
    {
        var log = new CallLog();
        var refused = Doc("Passport", new DateOnly(2026, 6, 1));
        var second = Doc("Insurance", new DateOnly(2026, 7, 1));
        var scheduler = new FakeReminderScheduler(log);
        scheduler.FailToSchedule.Add(refused.Id);
        var service = new DocumentAppService(
            new FakeDocumentRepository(log, refused, second), scheduler);

        await service.ReplanAllRemindersAsync(NowLocal); // no throw

        Assert.False(scheduler.Pending.ContainsKey(refused.Id));
        Assert.Equal(
            new ReminderInstruction.At(new DateTime(2026, 6, 1, 9, 0, 0)),
            scheduler.Pending[second.Id]);
    }

    [Fact]
    public async Task An_empty_store_schedules_nothing()
    {
        var log = new CallLog();
        var scheduler = new FakeReminderScheduler(log);
        var service = new DocumentAppService(new FakeDocumentRepository(log), scheduler);

        await service.ReplanAllRemindersAsync(NowLocal);

        Assert.Equal(["documents.GetAll"], log.Calls);
        Assert.Empty(scheduler.Scheduled);
    }
}
