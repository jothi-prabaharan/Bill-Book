using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Foreign Currency Gain or Loss report (TK-220): the exchange gains and
// losses on foreign-currency balances. Realized is what settlements posted to
// Realized FX Gain/Loss in a period; unrealized is what the balances still open
// would gain or lose if revalued at a rate the user gives, and nothing is posted
// for it. Benchmarked on Xero's Foreign Currency Gains and Losses, AutoCount's
// Unrealized Gain/Loss and Odoo's Unrealized Currency Gains/Losses. Every figure
// but the foreign amount is in the branch's base currency.

/// <summary>
/// What the report is asked for: the period the realized part covers and the date
/// the unrealized part is worked out at, both required (D-67); optionally one
/// currency; and the revaluation rate for each currency, in base currency per unit
/// of the foreign one, written <c>Rates[USD]=83.25</c>. The branch is always the
/// token's: a request naming another branch is refused with 403.
/// </summary>
public sealed class ForeignCurrencyGainLossReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "As at date is required.")]
    public DateOnly? AsAtDate { get; set; }

    /// <summary>Only this currency; every foreign currency when blank.</summary>
    [MaxLength(3, ErrorMessage = "Currency must be a 3-letter code.")]
    public string? CurrencyCode { get; set; }

    /// <summary>
    /// The revaluation rate for each currency: base currency per one unit of it.
    /// A currency with none still has its balances and its realized figure, and no
    /// unrealized one.
    /// </summary>
    public Dictionary<string, decimal>? Rates { get; set; }

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One account's foreign-currency balance, and what it has gained or lost.</summary>
public sealed class ForeignCurrencyAccountDto
{
    public long AccountId { get; set; }

    [MaxLength(100, ErrorMessage = "Account name cannot exceed 100 characters.")]
    public string AccountName { get; set; } = string.Empty;

    public ForeignAccountKind Kind { get; set; }

    /// <summary>
    /// What is owed to the branch in the currency, as debits less credits: positive
    /// for a receivable or money held, negative for a payable.
    /// </summary>
    public decimal DueSource { get; set; }

    /// <summary>The same balance in base currency at the rates the documents were booked at.</summary>
    public decimal CarryingBase { get; set; }

    /// <summary>The balance at the revaluation rate; null when the currency has no rate.</summary>
    public decimal? RevaluedBase { get; set; }

    /// <summary>
    /// Revalued less carrying: positive is a gain, negative a loss, whichever side of
    /// the books the balance is on. Null when the currency has no rate.
    /// </summary>
    public decimal? Unrealized { get; set; }

    /// <summary>What settlements in the period posted to Realized FX Gain/Loss for this account: positive is a gain.</summary>
    public decimal Realized { get; set; }
}

/// <summary>One currency: its accounts, and what the currency came to in total.</summary>
public sealed class ForeignCurrencyBlockDto
{
    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>The revaluation rate used, in base currency per unit; null when none was given.</summary>
    public decimal? Rate { get; set; }

    /// <summary>The currency has a balance and no rate, so its unrealized figure could not be worked out.</summary>
    public bool NeedsRate { get; set; }

    public List<ForeignCurrencyAccountDto> Accounts { get; set; } = [];

    public decimal DueSource { get; set; }

    public decimal CarryingBase { get; set; }

    public decimal? RevaluedBase { get; set; }

    public decimal? Unrealized { get; set; }

    public decimal Realized { get; set; }

    /// <summary>Realized plus unrealized; a currency with no rate counts only its realized part.</summary>
    public decimal Net { get; set; }
}

/// <summary>The currencies, with the totals in base currency.</summary>
public sealed class ForeignCurrencyGainLossReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly AsAtDate { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    public List<ForeignCurrencyBlockDto> Currencies { get; set; } = [];

    /// <summary>
    /// What was posted to Realized FX Gain/Loss in the period by a document with no
    /// foreign-currency balance, such as a manual journal, so it belongs to no
    /// currency. Counted in <see cref="TotalRealized"/>; blank when one currency is chosen.
    /// </summary>
    public decimal UnassignedRealized { get; set; }

    /// <summary>Every posting to Realized FX Gain/Loss in the period: the account's movement on the Profit and Loss.</summary>
    public decimal TotalRealized { get; set; }

    /// <summary>The unrealized figures of the currencies that have a rate.</summary>
    public decimal TotalUnrealized { get; set; }

    public decimal TotalNet { get; set; }

    /// <summary>The currencies with a balance and no rate, so the page can ask for one.</summary>
    public List<string> CurrenciesWithoutRate { get; set; } = [];
}

/// <summary>The date the choices are for: the page asks which currencies have a balance then.</summary>
public sealed class ForeignCurrencyChoicesRequest
{
    [Required(ErrorMessage = "As at date is required.")]
    public DateOnly? AsAtDate { get; set; }

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>A foreign currency the branch holds a balance in.</summary>
public sealed class ForeignCurrencyChoiceDto
{
    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>How many accounts hold a balance in it.</summary>
    public int Accounts { get; set; }

    /// <summary>
    /// The currency still has a balance, so a rate is asked for it. Always true on the
    /// summary's lookup, which lists only currencies with one; the details report lists every
    /// currency with a posting so a fully settled one can still be chosen.
    /// </summary>
    public bool HasBalance { get; set; }
}

/// <summary>The branch's base currency and the foreign currencies it holds a balance in at a date.</summary>
public sealed class ForeignCurrencyChoicesDto
{
    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    public List<ForeignCurrencyChoiceDto> Currencies { get; set; } = [];
}
