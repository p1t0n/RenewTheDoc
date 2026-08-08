using RenewTheDoc.Core;

namespace RenewTheDoc.Core.Tests;

public class DocumentStateTests
{
    private static readonly DateOnly Today = new(2026, 8, 8);

    private static Document Doc(DateOnly expiry, int remindDays = 30) => new()
    {
        Name = "Passport",
        ExpiryDate = expiry,
        RemindBefore = new RemindBefore(remindDays),
    };

    [Fact]
    public void Expiry_in_the_past_is_expired() =>
        Assert.Equal(DocumentState.Expired, Doc(Today.AddDays(-1)).GetState(Today));

    [Fact]
    public void Expiry_today_is_not_yet_expired() =>
        Assert.Equal(DocumentState.ExpiringSoon, Doc(Today).GetState(Today));

    [Fact]
    public void Inside_remind_before_window_is_expiring_soon() =>
        Assert.Equal(DocumentState.ExpiringSoon, Doc(Today.AddDays(30)).GetState(Today));

    [Fact]
    public void Outside_remind_before_window_is_ok() =>
        Assert.Equal(DocumentState.Ok, Doc(Today.AddDays(31)).GetState(Today));

    [Fact]
    public void List_orders_expired_first_then_nearest_expiry()
    {
        var far = Doc(Today.AddDays(300));
        var near = Doc(Today.AddDays(10));
        var expired = Doc(Today.AddDays(-5));

        var sorted = DocumentListOrder.Sorted([far, near, expired], Today);

        Assert.Equal([expired, near, far], sorted);
    }
}
