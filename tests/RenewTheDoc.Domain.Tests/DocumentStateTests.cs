using RenewTheDoc.Domain.Documents;

namespace RenewTheDoc.Domain.Tests;

public class DocumentStateTests
{
    private static readonly DateOnly Today = new(2026, 8, 8);

    private static Document Doc(DateOnly expiry, int remindDays = 30) =>
        Document.Create("Passport", expiry, new RemindBefore(remindDays));

    [Fact]
    public void Expiry_in_the_past_is_expired() =>
        Assert.Equal(DocumentState.Expired, Doc(Today.AddDays(-1)).StateOn(Today));

    [Fact]
    public void Expiry_today_is_not_yet_expired() =>
        Assert.Equal(DocumentState.ExpiringSoon, Doc(Today).StateOn(Today));

    [Fact]
    public void Inside_remind_before_window_is_expiring_soon() =>
        Assert.Equal(DocumentState.ExpiringSoon, Doc(Today.AddDays(30)).StateOn(Today));

    [Fact]
    public void Outside_remind_before_window_is_ok() =>
        Assert.Equal(DocumentState.Ok, Doc(Today.AddDays(31)).StateOn(Today));
}
