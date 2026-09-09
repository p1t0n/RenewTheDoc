using RenewTheDoc.Domain.Documents;
using RenewTheDoc.Persistence.Notifications;

namespace RenewTheDoc.Persistence.Tests;

/// <summary>
/// Characterization of the pre-REN-54 mapping: exactly what the adapter did, pinned before it stops
/// doing it, so the behaviour change is visible in the diff rather than asserted in a commit message.
/// </summary>
/// <remarks>
/// These stay after the fix. The derived number is still the number a Document scheduled under on a
/// device that ran an older build, and clearing that stale notification is the only way an existing
/// install stops firing it.
/// </remarks>
public class DerivedNotificationNumberTests
{
    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", 0)]
    [InlineData("82692553-6eed-464a-b35d-c4b9645f10fe", 66537833)]
    [InlineData("32fec112-db2c-4686-994a-995dce1916aa", 66537833)]
    [InlineData("550e8400-e29b-41d4-a716-446655440000", 1922970729)]
    public void The_derived_number_is_the_folded_guid_hash(string id, int expected) =>
        Assert.Equal(expected, DerivedNotificationNumber.For(new DocumentId(Guid.Parse(id))));

    /// <summary>Never negative: <c>NotificationId</c> is an int and the fold masks the sign bit off.</summary>
    [Fact]
    public void The_derived_number_is_never_negative()
    {
        for (var i = 0; i < 10_000; i++)
            Assert.True(DerivedNotificationNumber.For(DocumentId.New()) >= 0);
    }

    /// <summary>
    /// The bug itself, as a fact rather than an argument. This pair was found by drawing random
    /// Guids until two folded to the same number — it took 37,393 of them, because the fold has only
    /// 2³¹ outcomes.
    /// </summary>
    [Fact]
    public void Two_different_documents_can_share_a_derived_number()
    {
        var one = new DocumentId(Guid.Parse("82692553-6eed-464a-b35d-c4b9645f10fe"));
        var other = new DocumentId(Guid.Parse("32fec112-db2c-4686-994a-995dce1916aa"));

        Assert.NotEqual(one, other);
        Assert.Equal(DerivedNotificationNumber.For(one), DerivedNotificationNumber.For(other));
    }

    /// <summary>
    /// And it is structural, not bad luck: the fold XORs the Guid's four 32-bit words, so moving the
    /// same bit from one word to another lands on the same number. Colliding ids can be built to
    /// order, which is why no amount of retrying id generation would have been a fix.
    /// </summary>
    [Fact]
    public void Colliding_ids_can_be_constructed_on_purpose()
    {
        Assert.Equal(
            DerivedNotificationNumber.For(IdWithByteSet(0)),
            DerivedNotificationNumber.For(IdWithByteSet(4)));

        static DocumentId IdWithByteSet(int index)
        {
            var bytes = new byte[16];
            bytes[index] = 1;
            return new DocumentId(new Guid(bytes));
        }
    }
}
