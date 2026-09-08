using RenewTheDoc.Domain.Documents;

namespace RenewTheDoc.Domain.Tests;

public class RemindBeforeTests
{
    [Fact]
    public void Negative_days_violate_a_domain_rule_by_code()
    {
        var violation = Assert.Throws<DomainRuleViolationException>(() => new RemindBefore(-1));

        Assert.Equal(DomainRule.RemindBeforeNegative, violation.Rule);
    }

    [Fact]
    public void Zero_days_means_remind_on_the_expiry_date_itself() =>
        Assert.Equal(0, new RemindBefore(0).Days);
}
