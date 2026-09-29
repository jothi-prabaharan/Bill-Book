using System.Globalization;
using System.Text.RegularExpressions;
using Accounting.Entity.Enums;
using Accounting.Entity.TableEntities;

namespace Accounting.Repository.Repositories;

/// <summary>A lower-deduction certificate on the contact (section 197): a rate, a period and a limit.</summary>
public sealed record TdsCertificate(string Number, decimal Rate, DateOnly ValidFrom, DateOnly ValidTo, decimal Limit);

/// <summary>Who is being deducted from: their PAN and any certificate they hold.</summary>
public sealed record TdsDeductee(string? Pan, TdsCertificate? Certificate);

/// <summary>An earlier register row for the same vendor, section and year.</summary>
public sealed record TdsHistoryRow(
    TdsEntryKind Kind,
    decimal Amount,
    decimal CoveredByAdvance,
    decimal TdsBase,
    string? CertificateNo,
    decimal CertificateBase);

/// <summary>What a document owes in TDS and why.</summary>
public sealed record TdsComputation(
    decimal Amount,
    decimal CoveredByAdvance,
    decimal TdsBase,
    decimal Rate,
    TdsDeducteeKind DeducteeKind,
    string? CertificateNo,
    decimal CertificateBase,
    decimal? CertificateRate,
    decimal TdsAmount,
    string Note);

/// <summary>
/// Computes the TDS on one document (TK-117). Pure.
///
/// <list type="number">
/// <item><b>What counts.</b> The document's amount before GST. A bill after an
/// advance that already bore TDS counts only the part the advance did not
/// cover, so no rupee is taxed twice.</item>
/// <item><b>When.</b> A document above the single threshold is taxed on its
/// own. Once the year's total passes the aggregate threshold, the crossing
/// document is taxed on everything in the year not yet taxed — itself and the
/// earlier documents below the threshold — and every later one on itself
/// (<see cref="TdsThresholdBasis.WholeAmount"/>). Under
/// <see cref="TdsThresholdBasis.ExcessOnly"/> (194Q) only the part above the
/// threshold is ever taxed. A section with neither threshold taxes every
/// document.</item>
/// <item><b>At what rate.</b> By the PAN's fourth character: P or H take the
/// individual rate, others the other rate, and no valid PAN the section 206AA
/// rate. A lower-deduction certificate valid on the date takes its own rate
/// for as much of the base as its limit still allows; the rest is taxed at the
/// section rate.</item>
/// <item><b>Rounding.</b> To the nearest rupee, as section 288B requires.</item>
/// </list>
/// </summary>
public static partial class TdsCalculationRepository
{
    [GeneratedRegex("^[A-Z]{5}[0-9]{4}[A-Z]$")]
    private static partial Regex PanPattern();

    [GeneratedRegex("^[A-Z]{4}[0-9]{5}[A-Z]$")]
    private static partial Regex TanPattern();

    public static bool IsValidPan(string? pan) => pan is not null && PanPattern().IsMatch(pan.Trim().ToUpperInvariant());

    public static bool IsValidTan(string? tan) => tan is not null && TanPattern().IsMatch(tan.Trim().ToUpperInvariant());

    public static TdsDeducteeKind KindOf(string? pan)
    {
        if (!IsValidPan(pan))
        {
            return TdsDeducteeKind.NoPan;
        }

        char holder = pan!.Trim().ToUpperInvariant()[3];
        return holder is 'P' or 'H' ? TdsDeducteeKind.IndividualOrHuf : TdsDeducteeKind.Other;
    }

    /// <summary>The April-to-March year a date falls in, named by its first calendar year.</summary>
    public static int FinancialYearOf(DateOnly date) => date.Month >= 4 ? date.Year : date.Year - 1;

    public static decimal RateFor(TdsSection section, TdsDeducteeKind kind) => kind switch
    {
        TdsDeducteeKind.IndividualOrHuf => section.RateIndividual,
        TdsDeducteeKind.Other => section.RateOther,
        _ => section.RateNoPan,
    };

    public static TdsComputation Compute(
        TdsSection section,
        TdsDeductee deductee,
        IReadOnlyList<TdsHistoryRow> history,
        TdsEntryKind kind,
        decimal amount,
        DateOnly date)
    {
        TdsDeducteeKind deducteeKind = KindOf(deductee.Pan);
        decimal rate = RateFor(section, deducteeKind);

        // An advance that bore TDS covers the bill that follows it.
        decimal advancePool = history.Where(h => h.Kind == TdsEntryKind.Advance).Sum(h => h.Amount)
            - history.Where(h => h.Kind == TdsEntryKind.Credit).Sum(h => h.CoveredByAdvance);
        decimal covered = kind == TdsEntryKind.Credit ? Math.Clamp(advancePool, 0m, amount) : 0m;
        decimal counted = amount - covered;

        decimal before = history.Sum(h => h.Amount - h.CoveredByAdvance);
        decimal after = before + counted;
        decimal taxedBefore = history.Sum(h => h.TdsBase);

        decimal tdsBase;
        string when;

        if (section.SingleThreshold is null && section.AggregateThreshold is null)
        {
            tdsBase = counted;
            when = $"{section.SectionCode} has no threshold.";
        }
        else if (section.ThresholdBasis == TdsThresholdBasis.ExcessOnly && section.AggregateThreshold is decimal cap)
        {
            tdsBase = Math.Max(0m, Math.Max(0m, after - cap) - taxedBefore);
            when = tdsBase > 0
                ? $"{Money(after)} this year, above the {Money(cap)} threshold; only the excess is taxed."
                : $"{Money(after)} this year, within the {Money(cap)} threshold.";
        }
        else if (section.AggregateThreshold is decimal aggregate && after > aggregate)
        {
            // The crossing document catches up on everything not yet taxed.
            tdsBase = Math.Max(0m, after - taxedBefore);
            when = before > aggregate
                ? $"{Money(after)} this year, past the {Money(aggregate)} threshold."
                : $"This takes the year to {Money(after)}, past the {Money(aggregate)} threshold; "
                    + $"{Money(tdsBase)} not yet taxed is taxed now.";
        }
        else if (section.SingleThreshold is decimal single && counted > single)
        {
            tdsBase = counted;
            when = $"{Money(counted)} is above the {Money(single)} single-payment threshold.";
        }
        else
        {
            tdsBase = 0m;
            when = section.AggregateThreshold is decimal year
                ? $"Below the {section.SectionCode} thresholds: {Money(after)} of {Money(year)} this year."
                : $"Below the {section.SectionCode} threshold.";
        }

        if (covered > 0)
        {
            when += $" {Money(covered)} was taxed on an earlier advance.";
        }

        // A certificate valid on the date takes as much of the base as its limit still allows.
        decimal certificateBase = 0m;
        TdsCertificate? certificate = deductee.Certificate;
        bool certificateApplies = certificate is not null && tdsBase > 0
            && date >= certificate.ValidFrom && date <= certificate.ValidTo;

        if (certificateApplies)
        {
            decimal used = history.Where(h => h.CertificateNo == certificate!.Number).Sum(h => h.CertificateBase);
            certificateBase = Math.Clamp(certificate!.Limit - used, 0m, tdsBase);
        }

        decimal tds = Math.Round(
            certificateBase * (certificateApplies ? certificate!.Rate : 0m) / 100m
                + (tdsBase - certificateBase) * rate / 100m,
            0, MidpointRounding.AwayFromZero);

        string how = tdsBase == 0
            ? string.Empty
            : certificateBase > 0
                ? $" Certificate {certificate!.Number}: {Percent(certificate.Rate)} on {Money(certificateBase)}"
                    + (certificateBase < tdsBase ? $", {Percent(rate)} on the rest past its limit." : ".")
                : deducteeKind == TdsDeducteeKind.NoPan
                    ? $" No valid PAN, so {Percent(rate)} under section 206AA."
                    : $" {Percent(rate)} of {Money(tdsBase)}.";

        string note = (when + how).Trim();

        return new TdsComputation(
            amount,
            covered,
            tdsBase,
            rate,
            deducteeKind,
            certificateBase > 0 ? certificate!.Number : null,
            certificateBase,
            certificateBase > 0 ? certificate!.Rate : null,
            tds,
            note.Length <= 300 ? note : note[..300]);
    }

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

    private static string Percent(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture) + "%";
}
