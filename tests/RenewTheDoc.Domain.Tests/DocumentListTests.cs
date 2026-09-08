using RenewTheDoc.Domain.Documents;

namespace RenewTheDoc.Domain.Tests;

public class DocumentListTests
{
    private static readonly DateOnly Today = new(2026, 8, 8);

    private static Document Doc(string name, DateOnly expiry, int remindDays = 30) =>
        Document.Create(name, expiry, new RemindBefore(remindDays));

    [Fact]
    public void Groups_run_in_glossary_order_expired_first()
    {
        var ok = Doc("Ok", Today.AddDays(300));
        var soon = Doc("Soon", Today.AddDays(10));
        var expired = Doc("Expired", Today.AddDays(-5));

        var groups = DocumentList.Grouped([ok, soon, expired], Today);

        Assert.Equal(
            [DocumentState.Expired, DocumentState.ExpiringSoon, DocumentState.Ok],
            groups.Select(g => g.State));
        Assert.Equal([expired], groups[0].Documents);
        Assert.Equal([soon], groups[1].Documents);
        Assert.Equal([ok], groups[2].Documents);
    }

    [Fact]
    public void Inside_a_group_the_nearest_expiry_comes_first()
    {
        var later = Doc("Later", Today.AddDays(300));
        var sooner = Doc("Sooner", Today.AddDays(100));

        var groups = DocumentList.Grouped([later, sooner], Today);

        Assert.Equal([sooner, later], Assert.Single(groups).Documents);
    }

    [Fact]
    public void A_state_nobody_is_in_yields_no_group()
    {
        var groups = DocumentList.Grouped([Doc("Ok", Today.AddDays(300))], Today);

        Assert.Equal(DocumentState.Ok, Assert.Single(groups).State);
    }

    [Fact]
    public void Nothing_groups_into_nothing() =>
        Assert.Empty(DocumentList.Grouped([], Today));
}
