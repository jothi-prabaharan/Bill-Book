using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Foreign Currency Gain or Loss Details report (TK-221): the gains and losses of
// the Foreign Currency Gain or Loss report (TK-220), document by document. Which
// settlement made each realized amount, and which open document carries each
// unrealized one. Its currency and account totals are that report's, worked out by
// the same code, so the two cannot disagree. Benchmarked on Xero's Foreign Currency
// Gains and Losses detail and AutoCount's Realized Gain/Loss Listing.

/// <summary>
/// What the report is asked for: the period the realized part covers and, at its end,
/// the date the unrealized part is worked out at, both required (D-67); optionally one
/// currency, one contact and one account; and the revaluation rate for each currency,
/// in base currency per unit of the foreign one, written <c>Rates[USD]=83.25</c>. The
/// branch is always the token's: a request naming another branch is refused with 403.
/// </summary>
public sealed class ForeignCurrencyGainLossDetailsRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>Only this currency; every foreign currency when blank.</summary>
    [MaxLength(3, ErrorMessage = "Currency must be a 3-letter code.")]
    public string? CurrencyCode { get; set; }

    /// <summary>Only this contact's documents.</summary>
    public long? ContactId { get; set; }

    /// <summary>Only this account: a receivable, payable or foreign bank account.</summary>
    public long? AccountId { get; set; }

    /// <summary>The revaluation rate for each currency: base currency per one unit of it.</summary>
    public Dictionary<string, decimal>? Rates { get; set; }

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One document, or one settlement, under an account.</summary>
public sealed class ForeignCurrencyDocumentDto
{
    public ForeignDocumentKind Kind { get; set; }

    /// <summary>The document's date, or the settlement's date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>The kind of document: INV, BIL, RCM, SPM and so on.</summary>
    [MaxLength(3, ErrorMessage = "Type code cannot exceed 3 characters.")]
    public string TypeCode { get; set; } = string.Empty;

    [MaxLength(40, ErrorMessage = "Document number cannot exceed 40 characters.")]
    public string? DocumentNo { get; set; }

    public long? ContactId { get; set; }

    [MaxLength(150, ErrorMessage = "Contact name cannot exceed 150 characters.")]
    public string? ContactName { get; set; }

    /// <summary>For a settlement, the document it settled, such as <c>Settles INV-0042</c>.</summary>
    [MaxLength(80, ErrorMessage = "Reference cannot exceed 80 characters.")]
    public string? Reference { get; set; }

    /// <summary>
    /// Open: what is owed in the currency, negative for a payable. Settlement: what was
    /// settled, positive. Null when the settlement's balance leg is not in the books.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>Base currency per unit at the rate the document was booked at; null when nothing is owed.</summary>
    public decimal? BookedRate { get; set; }

    /// <summary>
    /// Open: the revaluation rate, null when the currency has none. Settlement: the rate
    /// the money moved at.
    /// </summary>
    public decimal? CurrentRate { get; set; }

    /// <summary>The amount in base currency at the booked rate.</summary>
    public decimal? CarryingBase { get; set; }

    /// <summary>Open: the amount at the revaluation rate. Settlement: at the rate the money moved at.</summary>
    public decimal? CurrentBase { get; set; }

    /// <summary>A settlement's exchange difference: positive is a gain. Null on an open document.</summary>
    public decimal? Realized { get; set; }

    /// <summary>An open document's difference from its booked rate: positive is a gain. Null on a settlement, or without a rate.</summary>
    public decimal? Unrealized { get; set; }
}

/// <summary>One account's balance in a currency and the documents behind it.</summary>
public sealed class ForeignCurrencyDetailAccountDto
{
    public long AccountId { get; set; }

    [MaxLength(100, ErrorMessage = "Account name cannot exceed 100 characters.")]
    public string AccountName { get; set; } = string.Empty;

    public ForeignAccountKind Kind { get; set; }

    public decimal DueSource { get; set; }

    public decimal CarryingBase { get; set; }

    public decimal? RevaluedBase { get; set; }

    public decimal? Unrealized { get; set; }

    public decimal Realized { get; set; }

    /// <summary>Open documents and settlements, by date.</summary>
    public List<ForeignCurrencyDocumentDto> Documents { get; set; } = [];
}

/// <summary>One currency: its accounts, and what the currency came to in total.</summary>
public sealed class ForeignCurrencyDetailBlockDto
{
    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = string.Empty;

    public decimal? Rate { get; set; }

    public bool NeedsRate { get; set; }

    public List<ForeignCurrencyDetailAccountDto> Accounts { get; set; } = [];

    public decimal DueSource { get; set; }

    public decimal CarryingBase { get; set; }

    public decimal? RevaluedBase { get; set; }

    public decimal? Unrealized { get; set; }

    public decimal Realized { get; set; }

    public decimal Net { get; set; }
}

/// <summary>The currencies, with the totals in base currency.</summary>
public sealed class ForeignCurrencyGainLossDetailsResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    public List<ForeignCurrencyDetailBlockDto> Currencies { get; set; } = [];

    /// <summary>The postings to Realized FX Gain/Loss that no currency can claim, such as a manual journal's.</summary>
    public List<ForeignCurrencyDocumentDto> Unassigned { get; set; } = [];

    public decimal UnassignedRealized { get; set; }

    public decimal TotalRealized { get; set; }

    public decimal TotalUnrealized { get; set; }

    public decimal TotalNet { get; set; }

    public List<string> CurrenciesWithoutRate { get; set; } = [];
}

/// <summary>The date the choices are for: which currencies, accounts and contacts have a foreign balance up to it.</summary>
public sealed class ForeignCurrencyDetailChoicesRequest
{
    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>An account that has a foreign-currency posting.</summary>
public sealed class ForeignCurrencyAccountChoiceDto
{
    public long AccountId { get; set; }

    [MaxLength(100, ErrorMessage = "Account name cannot exceed 100 characters.")]
    public string AccountName { get; set; } = string.Empty;

    public ForeignAccountKind Kind { get; set; }
}

/// <summary>A contact that has a foreign-currency posting.</summary>
public sealed class ForeignCurrencyContactChoiceDto
{
    public long ContactId { get; set; }

    [MaxLength(150, ErrorMessage = "Contact name cannot exceed 150 characters.")]
    public string ContactName { get; set; } = string.Empty;
}

/// <summary>The base currency, and the currencies, accounts and contacts the filters offer.</summary>
public sealed class ForeignCurrencyDetailChoicesDto
{
    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    public List<ForeignCurrencyChoiceDto> Currencies { get; set; } = [];

    public List<ForeignCurrencyAccountChoiceDto> Accounts { get; set; } = [];

    public List<ForeignCurrencyContactChoiceDto> Contacts { get; set; } = [];
}
