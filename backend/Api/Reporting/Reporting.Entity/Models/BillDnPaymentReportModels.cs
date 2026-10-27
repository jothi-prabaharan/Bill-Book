using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Bill/DN Payment Report (TK-238): what was paid to vendors in a period and which bills each
// payment settled. Benchmarked on AutoCount's Creditor Payment Listing, Xero's supplier payments
// and Odoo's payments to vendors. A payment is in its own currency, with its amount in the
// branch's base currency beside it; the totals are in base currency. The documents a payment
// settles are bills, and whatever else it has been allocated to (an opening balance, say), by
// their own number. A debit note is applied to a bill, not paid, so it is not listed here.

/// <summary>
/// What the report is asked for: the payment-date period, both dates required (D-67), optionally
/// one vendor, one bank account and one payment method, a page of payments at a time. The branch
/// is always the token's: a request naming another branch is refused with 403.
/// </summary>
public sealed class BillDnPaymentReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>Only this vendor; every vendor when blank.</summary>
    public long? ContactId { get; set; }

    /// <summary>Only payments made from this bank account; every account when blank.</summary>
    public long? BankAccountId { get; set; }

    /// <summary>Only payments made this way; every method when blank.</summary>
    public PaymentMethod? PaymentMethod { get; set; }

    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 25;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>A document a payment settled, with what it settled of it, in the payment's own currency.</summary>
public sealed class PaidDocumentDto
{
    /// <summary>BIL for a bill; the code the settlement names otherwise.</summary>
    [MaxLength(3, ErrorMessage = "Transaction type code cannot exceed 3 characters.")]
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    /// <summary>The bill's number; for a document with no number of its own, its code and id.</summary>
    [MaxLength(30, ErrorMessage = "Document number cannot exceed 30 characters.")]
    public string DocumentNo { get; set; } = string.Empty;

    /// <summary>The vendor's own number for the bill; null for any other document.</summary>
    [MaxLength(50, ErrorMessage = "Vendor bill number cannot exceed 50 characters.")]
    public string? VendorBillNo { get; set; }

    /// <summary>The bill's date; null where the document has none to show.</summary>
    public DateOnly? DocumentDate { get; set; }

    /// <summary>The day the settlement took effect.</summary>
    public DateOnly AllocationDate { get; set; }

    public decimal Amount { get; set; }
}

/// <summary>One posted payment, with the documents it settled and what it did not.</summary>
public sealed class VendorPaymentDto
{
    public long SpendMoneyId { get; set; }

    [MaxLength(30, ErrorMessage = "Payment number cannot exceed 30 characters.")]
    public string PaymentNo { get; set; } = string.Empty;

    public DateOnly PaymentDate { get; set; }

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

    /// <summary>What left the bank, in the payment's own currency.</summary>
    public decimal Amount { get; set; }

    /// <summary>The same amount in the branch's base currency.</summary>
    public decimal AmountBase { get; set; }

    /// <summary>What was withheld from the vendor as TDS, in the payment's own currency; nought when none.</summary>
    public decimal TdsAmount { get; set; }

    /// <summary>What went to the documents below, in the payment's own currency.</summary>
    public decimal Settled { get; set; }

    /// <summary>What was paid (with the TDS) and settled nothing, such as an advance, in the payment's own currency.</summary>
    public decimal Unallocated { get; set; }

    public List<PaidDocumentDto> Documents { get; set; } = [];
}

/// <summary>Every payment the filters match added up, in base currency.</summary>
public sealed class BillDnPaymentTotalsDto
{
    public int Payments { get; set; }

    public decimal Paid { get; set; }

    public decimal Unallocated { get; set; }
}

/// <summary>What one vendor was paid in the period, in base currency.</summary>
public sealed class VendorPaidDto
{
    public long ContactId { get; set; }

    [MaxLength(50, ErrorMessage = "Contact code cannot exceed 50 characters.")]
    public string ContactCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Contact name cannot exceed 150 characters.")]
    public string ContactName { get; set; } = string.Empty;

    public int Payments { get; set; }

    public decimal Paid { get; set; }
}

public sealed class BillDnPaymentReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    /// <summary>This page of payments, newest first.</summary>
    public List<VendorPaymentDto> Payments { get; set; } = [];

    /// <summary>How many payments the filters match, all pages.</summary>
    public int TotalPayments { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    /// <summary>Added up over every payment the filters match, not only this page.</summary>
    public BillDnPaymentTotalsDto Totals { get; set; } = new();

    /// <summary>The vendors paid most, largest first, at most ten.</summary>
    public List<VendorPaidDto> TopVendors { get; set; } = [];

    /// <summary>How many more vendors were paid in the period than are listed.</summary>
    public int OtherVendors { get; set; }

    /// <summary>What those other vendors were paid, in base currency.</summary>
    public decimal OtherVendorsPaid { get; set; }
}

/// <summary>A bank account the filter can pick.</summary>
public sealed class PaymentBankAccountChoiceDto
{
    public long BankAccountId { get; set; }

    [MaxLength(150, ErrorMessage = "Bank account name cannot exceed 150 characters.")]
    public string AccountName { get; set; } = string.Empty;

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = string.Empty;
}
