using Accounting.Entity.Enums;

namespace Accounting.Repository.Repositories;

/// <summary>
/// The arithmetic of a savings scheme (TK-141), pure so it can be tested
/// without a database. The decisions it encodes are settled in TK-141:
///
/// <list type="bullet">
/// <item><b>The bonus is the jeweller's expense, recognised when it is spent.</b>
/// At redemption a system journal debits <c>Savings Scheme Bonus</c> and credits
/// the customer's receivable, allocated to the invoice like a credit note. The
/// invoice itself is at full value, so GST is charged on everything the customer
/// takes home and never on less.</item>
/// <item><b>A lapsed or early-closed enrolment earns no bonus.</b> What was paid is
/// still the customer's: redeemable against an invoice, or refundable.</item>
/// <item><b>A refund returns what was paid and never the bonus</b>, with nothing
/// deducted. A matured enrolment refunded rather than redeemed forfeits its bonus.</item>
/// <item><b>Bonus is applied first, then what was paid.</b> Bonus beyond the
/// invoice is forfeited; paid money beyond it stays on the customer's advance.</item>
/// </list>
/// </summary>
public static class SavingsSchemeRulesRepository
{
    public static decimal Bonus(SchemeBonusType type, decimal value, decimal instalment, int months) =>
        Math.Round(type switch
        {
            SchemeBonusType.FixedAmount => value,
            SchemeBonusType.Instalments => value * instalment,
            SchemeBonusType.Percent => instalment * months * value / 100m,
            _ => 0m,
        }, 2, MidpointRounding.AwayFromZero);

    /// <summary>The instalments' due dates: one a month from the start date, counted from it.</summary>
    public static IEnumerable<(int Number, DateOnly Due)> Schedule(DateOnly start, int months) =>
        Enumerable.Range(1, months).Select(n => (n, start.AddMonths(n - 1)));

    /// <summary>An enrolment lapses when its earliest unpaid instalment is more than its grace days late.</summary>
    public static bool IsLapsed(DateOnly earliestUnpaidDue, int graceDays, DateOnly today) =>
        today > earliestUnpaidDue.AddDays(graceDays);

    /// <summary>What goes onto an invoice: the bonus first, then what was paid, neither past what the invoice still owes.</summary>
    public static (decimal Bonus, decimal Principal) Split(decimal bonus, decimal principal, decimal outstanding)
    {
        decimal room = Math.Max(0m, outstanding);
        decimal bonusApplied = Math.Min(Math.Max(0m, bonus), room);
        decimal principalApplied = Math.Min(Math.Max(0m, principal), room - bonusApplied);
        return (bonusApplied, principalApplied);
    }
}
