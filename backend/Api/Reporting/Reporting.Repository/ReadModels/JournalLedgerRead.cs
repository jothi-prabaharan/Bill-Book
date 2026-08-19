#pragma warning disable CS8618
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>acc.JournalLedger</c>, read-only. The single posting target — every
/// financial document in the system writes its double-entry legs there — so most
/// accounting reports start here.
///
/// One row is one leg: a debit <b>xor</b> a credit, never both, never negative.
/// A report that sums <c>DebitAmountBase</c> and subtracts
/// <c>CreditAmountBase</c> is doing the right thing; one that expects a signed
/// column is not.
///
/// <b>Base amounts against document amounts.</b> <c>DebitAmount</c> is what the
/// document said in its own currency; <c>DebitAmountBase</c> is the same leg in
/// the branch's base currency at the rate the document was booked at. Reports
/// offering a <c>(Source)</c> column mean the first and a <c>%CurCode%</c> column
/// the second — see Reporting.md §7.
/// </summary>
public class JournalLedgerRead : OrgScopedEntity
{
    public long LedgerId { get; set; }

    public DateOnly LedgerDate { get; set; }

    public long AccountId { get; set; }

    /// <summary>Null on legs with no sub-dimension — bank and equity.</summary>
    public long? SubAccountId { get; set; }

    public string TransactionTypeCode { get; set; }
    public long TransactionId { get; set; }

    public long TransactionDetailId { get; set; }

    public decimal DebitAmount { get; set; }

    public decimal CreditAmount { get; set; }

    public decimal DebitAmountBase { get; set; }

    public decimal CreditAmountBase { get; set; }

    public string CurrencyCode { get; set; }
    public decimal ExchangeRate { get; set; }

    /// <summary>
    /// Denormalized onto the leg. Present even where <c>SubAccountId</c> is null,
    /// which is what lets a contact report avoid a join it would otherwise need.
    /// </summary>
    public long? ContactId { get; set; }

    public int LedgerTypeId { get; set; }

    public int LedgerSourceId { get; set; }

    public string? TransactionDesc { get; set; }

    public long? JournalId { get; set; }
}


