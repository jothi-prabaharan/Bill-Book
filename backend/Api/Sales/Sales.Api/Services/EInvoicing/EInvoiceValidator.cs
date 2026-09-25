using System.Globalization;
using System.Text.RegularExpressions;

namespace Sales.Api.Services.EInvoicing;

/// <summary>A reason a document cannot be sent, in plain words naming the field (TK-91).</summary>
public sealed record EInvoiceProblem(string Code, string Message);

/// <summary>
/// Everything about an INV-01 document that can be checked without the IRP
/// (TK-91). The IRP's refusals are slow and each costs an attempt, so a
/// document that fails any of these goes to <c>Failed</c> with the problems
/// listed, and is never sent.
///
/// Pure: the date the check runs on is passed in.
/// </summary>
public static partial class EInvoiceValidator
{
    /// <summary>
    /// Totals add up "to the rupee": the IRP tolerates a difference of up to one
    /// rupee between a stated total and the sum of its parts.
    /// </summary>
    public const decimal Tolerance = 1.00m;

    /// <summary>The schema's cap on lines per document.</summary>
    public const int MaxItems = 1000;

    [GeneratedRegex("^[1-9A-Za-z][A-Za-z0-9/-]{0,15}$")]
    private static partial Regex DocumentNumber();

    [GeneratedRegex("^([0-9]{6}|[0-9]{8})$")]
    private static partial Regex HsnCode();

    [GeneratedRegex("^[0-9]{2}$")]
    private static partial Regex StateCode();

    /// <param name="document">The mapped document, exactly as it would be sent.</param>
    /// <param name="documentDate">The document's date.</param>
    /// <param name="today">The branch's today.</param>
    /// <param name="reportingWindowDays">
    /// How many days old a document the IRP still accepts for this branch, when
    /// the branch is a larger taxpayer with a notified window; null for none.
    /// </param>
    public static IReadOnlyList<EInvoiceProblem> Validate(
        Inv01Document document, DateOnly documentDate, DateOnly today, int? reportingWindowDays = null)
    {
        var problems = new List<EInvoiceProblem>();
        bool export = document.TranDtls.SupTyp.StartsWith("EXP", StringComparison.Ordinal);

        if (!DocumentNumber().IsMatch(document.DocDtls.No))
        {
            problems.Add(new("DOC_NO",
                "The document number must be 1 to 16 letters, digits, / or -, and cannot start with 0, / or -."));
        }

        if (documentDate > today)
        {
            problems.Add(new("DOC_DATE", "The document is dated in the future."));
        }
        else if (reportingWindowDays is int window && documentDate < today.AddDays(-window))
        {
            problems.Add(new("DOC_DATE",
                $"The IRP accepts documents up to {window} days old, and this one is dated "
                    + $"{documentDate.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}."));
        }

        CheckParty(problems, "SELLER", "the branch", document.SellerDtls, requireGstin: true);
        CheckParty(problems, "BUYER", "the customer", document.BuyerDtls, requireGstin: !export);

        if (!StateCode().IsMatch(document.BuyerDtls.Pos))
        {
            problems.Add(new("POS", "The place of supply is not set."));
        }

        if (document.ItemList.Count == 0)
        {
            problems.Add(new("ITEMS", "The document has no lines."));
        }
        else if (document.ItemList.Count > MaxItems)
        {
            problems.Add(new("ITEMS", $"The IRP takes at most {MaxItems} lines on one document."));
        }

        foreach (Inv01Item item in document.ItemList)
        {
            CheckItem(problems, item);
        }

        CheckTotals(problems, document);
        return problems;
    }

    private static void CheckParty(List<EInvoiceProblem> problems, string code, string who, Inv01Party party, bool requireGstin)
    {
        if (requireGstin)
        {
            if (!Shared.Kernel.Tax.Gstin.IsValid(party.Gstin))
            {
                problems.Add(new($"{code}_GSTIN", $"The GSTIN of {who} is missing or is not a valid GSTIN."));
            }
            else if (party.Stcd != Shared.Kernel.Tax.Gstin.StateCodeOf(party.Gstin))
            {
                problems.Add(new($"{code}_STATE",
                    $"The state of {who}'s address does not match the first two digits of its GSTIN."));
            }
        }

        if (string.IsNullOrWhiteSpace(party.LglNm) || party.LglNm.Trim().Length < 3)
        {
            problems.Add(new($"{code}_NAME", $"The legal name of {who} must be at least 3 characters."));
        }

        if (string.IsNullOrWhiteSpace(party.Addr1) || string.IsNullOrWhiteSpace(party.Loc) || party.Loc.Trim().Length < 3)
        {
            problems.Add(new($"{code}_ADDRESS", $"The address of {who} needs a first line and a city."));
        }

        if (party.Pin is < 100000 or > 999999)
        {
            problems.Add(new($"{code}_PIN", $"The PIN code of {who} must be six digits."));
        }

        if (!StateCode().IsMatch(party.Stcd))
        {
            problems.Add(new($"{code}_STATE", $"The state of {who}'s address is not set."));
        }
    }

    private static void CheckItem(List<EInvoiceProblem> problems, Inv01Item item)
    {
        string line = $"Line {item.SlNo}";

        if (!HsnCode().IsMatch(item.HsnCd))
        {
            problems.Add(new("HSN", $"{line} needs an HSN or SAC code of 6 or 8 digits."));
        }

        if (item.IsServc == "N" && string.IsNullOrWhiteSpace(item.Unit))
        {
            problems.Add(new("UQC", $"{line} has no GST unit (UQC). Set the UQC on its unit of measure."));
        }

        if (item.IsServc == "N" && item.Qty <= 0)
        {
            problems.Add(new("QTY", $"{line} needs a quantity above zero."));
        }

        if (Math.Abs(item.TotAmt - item.Discount - item.AssAmt) > Tolerance
            || Math.Abs(item.AssAmt + item.CgstAmt + item.SgstAmt + item.IgstAmt + item.CesAmt - item.TotItemVal) > Tolerance)
        {
            problems.Add(new("TOTALS", $"{line}'s amounts do not add up."));
        }

        if (item.IgstAmt > 0 && (item.CgstAmt > 0 || item.SgstAmt > 0))
        {
            problems.Add(new("TAX_SPLIT", $"{line} charges IGST beside CGST or SGST."));
        }
    }

    private static void CheckTotals(List<EInvoiceProblem> problems, Inv01Document document)
    {
        Inv01Values v = document.ValDtls;
        IReadOnlyList<Inv01Item> items = document.ItemList;

        bool linesAgree =
            Math.Abs(items.Sum(i => i.AssAmt) - v.AssVal) <= Tolerance
            && Math.Abs(items.Sum(i => i.CgstAmt) - v.CgstVal) <= Tolerance
            && Math.Abs(items.Sum(i => i.SgstAmt) - v.SgstVal) <= Tolerance
            && Math.Abs(items.Sum(i => i.IgstAmt) - v.IgstVal) <= Tolerance
            && Math.Abs(items.Sum(i => i.CesAmt) - v.CesVal) <= Tolerance;

        decimal computed = v.AssVal + v.CgstVal + v.SgstVal + v.IgstVal + v.CesVal + v.OthChrg - v.Discount + v.RndOffAmt;

        if (!linesAgree || Math.Abs(computed - v.TotInvVal) > Tolerance)
        {
            problems.Add(new("TOTALS",
                "The document's total does not match its lines. Open it, check the lines and the round-off, and save it again."));
        }

        if (v.RndOffAmt is <= -100m or >= 100m)
        {
            problems.Add(new("ROUND_OFF", "The round-off must be less than 100 either way."));
        }
    }
}
