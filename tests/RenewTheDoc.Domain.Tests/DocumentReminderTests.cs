using RenewTheDoc.Domain.Documents;

namespace RenewTheDoc.Domain.Tests;

public class DocumentReminderTests
{
    private static readonly DateTime Now = new(2026, 8, 8, 15, 0, 0); // 15:00 local

    private static Document Doc(DateOnly expiry, int remindDays = 30) =>
        Document.Create("ID card", expiry, new RemindBefore(remindDays));

    [Fact]
    public void Future_remind_moment_schedules_at_0900_local()
    {
        var plan = Doc(new DateOnly(2026, 12, 1)).PlanReminder(Now);

        var at = Assert.IsType<ReminderInstruction.At>(plan);
        Assert.Equal(new DateTime(2026, 11, 1, 9, 0, 0), at.LocalTime);
    }

    [Fact]
    public void Overdue_remind_moment_but_not_expired_fires_immediately()
    {
        var doc = Doc(new DateOnly(2026, 8, 11)); // expires in 3 days, window is 30
        Assert.IsType<ReminderInstruction.Immediate>(doc.PlanReminder(Now));
    }

    [Fact]
    public void Remind_moment_today_but_earlier_than_now_fires_immediately()
    {
        var doc = Doc(new DateOnly(2026, 9, 7)); // remind date = today, 09:00 < 15:00
        Assert.IsType<ReminderInstruction.Immediate>(doc.PlanReminder(Now));
    }

    [Fact]
    public void Already_expired_document_gets_no_reminder()
    {
        var doc = Doc(new DateOnly(2026, 8, 1));
        Assert.IsType<ReminderInstruction.None>(doc.PlanReminder(Now));
    }

    [Fact]
    public void An_edited_document_plans_from_its_new_expiry_date()
    {
        var doc = Doc(new DateOnly(2026, 12, 1));

        var edited = doc.Edit("ID card", new DateOnly(2027, 12, 1), new RemindBefore(30));

        var at = Assert.IsType<ReminderInstruction.At>(edited.PlanReminder(Now));
        Assert.Equal(new DateTime(2027, 11, 1, 9, 0, 0), at.LocalTime);
    }
}
