using System.ComponentModel.DataAnnotations;
using Sales.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Sales.Entity.TableEntities;

/// <summary>
/// One way a till sale was paid (TK-39): so much in cash, so much by card, so
/// much by UPI. A sale may be split across several.
///
/// Each tender names the bank or cash account (<c>acc.BankAccounts</c>) the
/// money lands in. Posting debits that account's ledger account instead of the
/// customer's receivable, so a paid till sale leaves nothing owing. The id is
/// Accounting's and is checked there, at posting, because Sales cannot read
/// Accounting's tables.
///
/// <see cref="Amount"/> is what was handed over for this tender. Change is
/// given only from cash, and the cash tender's ledger debit is its amount less
/// the invoice's <c>ChangeAmount</c>.
/// </summary>
public class InvoiceTender : OrgScopedEntity
{
    public long InvoiceTenderId { get; set; }

    public long InvoiceId { get; set; }

    public PosTenderMode Mode { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "A tender amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose the account the money went into.")]
    public long BankAccountId { get; set; }

    /// <summary>A card approval code or a UPI transaction reference.</summary>
    [MaxLength(50, ErrorMessage = "Reference cannot exceed 50 characters.")]
    public string? Reference { get; set; }
}
