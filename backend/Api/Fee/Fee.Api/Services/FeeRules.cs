using Fee.Entity.Enums;
using Fee.Entity.TableEntities;

namespace Fee.Api.Services;

/// <summary>
/// The fee arithmetic and the postings it makes (S4, TK-64). Pure, so every
/// rule and every leg is tested without a database or Accounting.
/// </summary>
public static class FeeRules
{
    public const string AccountsReceivable = "Accounts Receivable";
    public const string FeeIncome = "Fee Income";
    public const string DiscountGiven = "Discount Given";
    public const string RefundableDeposits = "Refundable Deposits";
    public const int ContactReference = 1;
    public const int TradePurpose = 0;
    public const int OverpaymentPurpose = 2;

    /// <summary>The year and month of <c>2026-06</c>.</summary>
    public static (int Year, int Month) Period(string periodKey) =>
        (int.Parse(periodKey[..4], System.Globalization.CultureInfo.InvariantCulture),
         int.Parse(periodKey[5..7], System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// Whether a line of this frequency falls due in <paramref name="month"/> of
    /// a school year that starts in <paramref name="firstMonth"/>.
    /// </summary>
    public static bool IsDue(FeeFrequency frequency, int firstMonth, int month)
    {
        int offset = ((month - firstMonth) % 12 + 12) % 12;
        return frequency switch
        {
            FeeFrequency.Monthly => true,
            FeeFrequency.Quarterly => offset % 3 == 0,
            FeeFrequency.Termly => offset is 0 or 4 or 8,
            _ => offset == 0,
        };
    }

    /// <summary>The due date: the line's day in the period's month.</summary>
    public static DateOnly DueDate(string periodKey, int dueDay)
    {
        (int year, int month) = Period(periodKey);
        return new DateOnly(year, month, Math.Clamp(dueDay, 1, 28));
    }

    /// <summary>A concession on an amount: a percentage rounded to the paisa, or a fixed sum never above the amount.</summary>
    public static decimal ConcessionOn(decimal amount, ConcessionKind kind, decimal value) => kind switch
    {
        ConcessionKind.Percent => Math.Min(amount, Math.Round(amount * value / 100m, 2, MidpointRounding.AwayFromZero)),
        _ => Math.Min(amount, value),
    };

    /// <summary>The concession that applies on a date: approved, in force, the largest if two overlap.</summary>
    public static decimal BestConcession(decimal amount, IEnumerable<FeeConcession> concessions, long feeHeadId, DateOnly on) =>
        concessions
            .Where(c => c.IsApproved && c.FeeHeadId == feeHeadId && c.ValidFrom <= on && c.ValidTo >= on)
            .Select(c => ConcessionOn(amount, c.ConcessionKind, c.Value))
            .DefaultIfEmpty(0m)
            .Max();

    /// <summary>
    /// The demand's postings: the guardian's receivable for the net, each
    /// head's income for its full amount, and each concession to Discount Given.
    /// Debits equal credits because net plus concessions is the total.
    /// </summary>
    public static List<LedgerLeg> DemandLegs(FeeDemand demand, IReadOnlyDictionary<long, FeeHead> heads)
    {
        var legs = new List<LedgerLeg>();
        if (demand.NetAmount > 0)
        {
            legs.Add(new LedgerLeg
            {
                LedgerTypeId = LedgerType.Control,
                AccountSystemName = AccountsReceivable,
                SubAccountReferenceType = ContactReference,
                SubAccountReferenceId = demand.ContactId,
                SubAccountPurpose = TradePurpose,
                DebitAmount = demand.NetAmount,
            });
        }

        foreach (FeeDemandLine line in demand.Lines)
        {
            FeeHead head = heads[line.FeeHeadId];
            legs.Add(new LedgerLeg
            {
                LedgerTypeId = LedgerType.Item,
                TransactionDetailId = line.FeeDemandLineId,
                AccountId = head.IncomeAccountId,
                AccountSystemName = head.IncomeAccountId is null ? (head.IsRefundable ? RefundableDeposits : FeeIncome) : null,
                CreditAmount = line.Amount,
            });

            if (line.ConcessionAmount > 0)
            {
                legs.Add(new LedgerLeg
                {
                    LedgerTypeId = LedgerType.Item,
                    TransactionDetailId = line.FeeDemandLineId,
                    AccountSystemName = DiscountGiven,
                    DebitAmount = line.ConcessionAmount,
                });
            }
        }

        return legs;
    }

    /// <summary>
    /// The receipt's postings: the bank for the whole amount, the guardian's
    /// receivable for what settles demands, and an overpayment advance for the
    /// rest, so the receivable itself ties to the open demands.
    /// </summary>
    public static List<LedgerLeg> ReceiptLegs(FeeReceipt receipt)
    {
        decimal allocated = receipt.Amount - receipt.UnallocatedAmount;
        var legs = new List<LedgerLeg>
        {
            new() { LedgerTypeId = LedgerType.Control, BankAccountId = receipt.BankAccountId, DebitAmount = receipt.Amount },
        };

        if (allocated > 0)
        {
            legs.Add(new LedgerLeg
            {
                LedgerTypeId = LedgerType.Control,
                AccountSystemName = AccountsReceivable,
                SubAccountReferenceType = ContactReference,
                SubAccountReferenceId = receipt.ContactId,
                SubAccountPurpose = TradePurpose,
                CreditAmount = allocated,
            });
        }

        if (receipt.UnallocatedAmount > 0)
        {
            legs.Add(new LedgerLeg
            {
                LedgerTypeId = LedgerType.Control,
                AccountSystemName = AccountsReceivable,
                SubAccountReferenceType = ContactReference,
                SubAccountReferenceId = receipt.ContactId,
                SubAccountPurpose = OverpaymentPurpose,
                CreditAmount = receipt.UnallocatedAmount,
            });
        }

        return legs;
    }

    /// <summary>Whether a set of legs balances: debits equal credits, and nothing is both or neither.</summary>
    public static bool Balances(IReadOnlyCollection<LedgerLeg> legs) =>
        legs.All(l => (l.DebitAmount > 0) != (l.CreditAmount > 0))
        && legs.Sum(l => l.DebitAmount) == legs.Sum(l => l.CreditAmount);

    /// <summary>
    /// Oldest open demands first, until the money runs out. Returns what goes
    /// to each; what is left over is the advance.
    /// </summary>
    public static List<(long FeeDemandId, decimal Amount)> AutoAllocate(decimal amount, IEnumerable<(long FeeDemandId, decimal Open)> oldestFirst)
    {
        var result = new List<(long, decimal)>();
        decimal left = amount;
        foreach ((long id, decimal open) in oldestFirst)
        {
            if (left <= 0)
            {
                break;
            }

            decimal take = Math.Min(left, open);
            if (take > 0)
            {
                result.Add((id, take));
                left -= take;
            }
        }

        return result;
    }

    /// <summary>
    /// Why a chosen allocation cannot stand, or null: each to an open demand of
    /// this guardian, none above what it still owes, and not more than was paid.
    /// </summary>
    public static string? AllocationProblem(decimal receiptAmount, IReadOnlyCollection<(long FeeDemandId, decimal Amount)> allocations, IReadOnlyDictionary<long, decimal> openByDemand)
    {
        if (allocations.Select(a => a.FeeDemandId).Distinct().Count() != allocations.Count)
        {
            return "A demand is listed twice.";
        }

        if (allocations.Any(a => !openByDemand.ContainsKey(a.FeeDemandId)))
        {
            return "Allocate only to posted demands of this guardian that are still open.";
        }

        if (allocations.Any(a => a.Amount > openByDemand[a.FeeDemandId]))
        {
            return "An allocation is more than the demand still owes.";
        }

        return allocations.Sum(a => a.Amount) > receiptAmount ? "The allocations add up to more than was received." : null;
    }
}
