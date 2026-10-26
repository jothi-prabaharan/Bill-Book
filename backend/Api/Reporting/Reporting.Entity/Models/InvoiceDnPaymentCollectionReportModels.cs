using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Invoice/DN Payment Collection Report (TK-227): what was collected in a period and which
// invoices each receipt settled. Benchmarked on AutoCount's Debtor Payment Collection, Xero's
// customer receipts and Odoo's payments by partner. A receipt is in its own currency, with its
// amount in the branch's base currency beside it; the totals are in base currency. Sales has no
// debit note yet, so the documents a receipt settles are invoices today and whatever else a
// receipt has been allocated to (an opening balance, say), by their own number.

/// <summary>
/// What the report is asked for: the receipt-date period, both dates required (D-67), optionally
/// one customer, one bank account and one payment method, a page of receipts at a time. The branch
/// is always the token's: a request naming another branch is refused with 403.
/// </summary>
public sealed class InvoiceDnPaymentCollectionReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>Only this customer; every customer when blank.</summary>
    public long? ContactId { get; set; }

    /// <summary>Only receipts paid into this bank account; every account when blank.</summary>
    public long? BankAccountId { get; set; }

    /// <summary>Only receipts taken this way; every method when blank.</summary>
    public PaymentMethod? PaymentMethod { get; set; }

    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 25;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>A document a receipt settled, with what it settled of it, in the receipt's own currency.</summary>
public sealed class SettledDocumentDto
{
    /// <summary>INV or POS for an invoice; the code the settlement names otherwise.</summary>
    [MaxLength(3, ErrorMessage = "Transaction type code cannot exceed 3 characters.")]
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    /// <summary>The invoice's number; for a document with no number of its own, its code and id.</summary>
    [MaxLength(30, ErrorMessage = "Document number cannot exceed 30 characters.")]
    public string DocumentNo { get; set; } = string.Empty;

    /// <summary>The invoice's date; null where the document has none to show.</summary>
    public DateOnly? DocumentDate { get; set; }

    /// <summary>The day the settlement took effect.</summary>
    public DateOnly AllocationDate { get; set; }

    public decimal Amount { get; set; }
}

/// <summary>One posted receipt, with the documents it settled and what it did not.</summary>
public sealed class ReceiptCollectionDto
{
    public long ReceiveMoneyId { get; set; }

    [MaxLength(30, ErrorMessage = "Receipt number cannot exceed 30 characters.")]
    public string ReceiptNo { get; set; } = string.Empty;

    public DateOnly ReceiptDate { get; set; }

    public long ContactId { get; set; }

    [MaxLength(50, ErrorMessage = "Contact code cannot exceed 50 characters.")]
    public string ContactCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Contact name cannot exceed 150 characters.")]
    public string ContactName { get; set; } = string.Empty;

    public long BankAccountId { get; set; }

    [MaxLength(150, ErrorMessage = "Bank account name cannot exceed 150 characters.")]
    public string BankAccountName { get; set; } = string.Empty;

    public PaymentMethod PaymentMethod { get; set; }

    [MaxLength(50, ErrorMessage = "Reference number cannot exceed 50 characters.")]
    public string? ReferenceNo { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>What reached the bank, in the receipt's own currency.</summary>
    public decimal Amount { get; set; }

    /// <summary>The same amount in the branch's base currency.</summary>
    public decimal AmountBase { get; set; }

    /// <summary>What the customer withheld as TDS, in the receipt's own currency; nought when none.</summary>
    public decimal TdsAmount { get; set; }

    /// <summary>What went to the documents below, in the receipt's own currency.</summary>
    public decimal Settled { get; set; }

    /// <summary>What was received (with the TDS) and settled nothing, such as an advance, in the receipt's own currency.</summary>
    public decimal Unallocated { get; set; }

    public List<SettledDocumentDto> Documents { get; set; } = [];
}

/// <summary>Every receipt the filters match added up, in base currency.</summary>
public sealed class InvoiceDnPaymentCollectionTotalsDto
{
    public int Receipts { get; set; }

    public decimal Collected { get; set; }

    public decimal Unallocated { get; set; }
}

/// <summary>What one customer paid in the period, in base currency.</summary>
public sealed class CustomerCollectionDto
{
    public long ContactId { get; set; }

    [MaxLength(50, ErrorMessage = "Contact code cannot exceed 50 characters.")]
    public string ContactCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Contact name cannot exceed 150 characters.")]
    public string ContactName { get; set; } = string.Empty;

    public int Receipts { get; set; }

    public decimal Collected { get; set; }
}

public sealed class InvoiceDnPaymentCollectionReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    /// <summary>This page of receipts, newest first.</summary>
    public List<ReceiptCollectionDto> Receipts { get; set; } = [];

    /// <summary>How many receipts the filters match, all pages.</summary>
    public int TotalReceipts { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    /// <summary>Added up over every receipt the filters match, not only this page.</summary>
    public InvoiceDnPaymentCollectionTotalsDto Totals { get; set; } = new();

    /// <summary>The customers who paid most, largest first, at most ten.</summary>
    public List<CustomerCollectionDto> TopCustomers { get; set; } = [];

    /// <summary>How many more customers paid in the period than are listed.</summary>
    public int OtherCustomers { get; set; }

    /// <summary>What those other customers paid, in base currency.</summary>
    public decimal OtherCustomersCollected { get; set; }
}

/// <summary>A bank account the filter can pick.</summary>
public sealed class CollectionBankAccountChoiceDto
{
    public long BankAccountId { get; set; }

    [MaxLength(150, ErrorMessage = "Bank account name cannot exceed 150 characters.")]
    public string AccountName { get; set; } = string.Empty;

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = string.Empty;
}
