using System.Text;
using Purchase.Entity.Enums;
using Purchase.Entity.TableEntities;

namespace Purchase.Repository.Repositories;

/// <summary>A posted bill as the purchase register totals it, for reconciliation.</summary>
public sealed record BooksBill(
    long BillId,
    string BillNo,
    string Gstin,
    string SupplierInvoiceNo,
    DateOnly SupplierInvoiceDate,
    decimal Taxable,
    decimal Tax,
    decimal EligibleTax,
    bool ReverseCharge);

/// <summary>What matching found: one line per invoice on either side, and the totals.</summary>
public sealed record Gstr2bMatchResult(IReadOnlyList<Gstr2bLine> Lines, decimal ItcAtRisk)
{
    public int Count(Gstr2bMatchState state) => Lines.Count(l => l.MatchState == state);
}

/// <summary>
/// Matches a month's GSTR-2B against the books (TK-116). Pure.
///
/// <list type="number">
/// <item>A 2B invoice matches a bill from the same supplier GSTIN with the same number, exactly first, then ignoring case, separators and leading zeros ("INV/007" is "inv-7"), which the line's note says.</item>
/// <item>A matched pair is <see cref="Gstr2bMatchState.Matched"/> when the taxable value and the tax agree within a rupee and the dates agree, and <see cref="Gstr2bMatchState.Mismatched"/> otherwise, with the difference in the note.</item>
/// <item>A 2B invoice no bill records is <see cref="Gstr2bMatchState.MissingInBooks"/>; a bill of the period 2B does not mention is <see cref="Gstr2bMatchState.MissingIn2b"/>.</item>
/// </list>
///
/// <b>The credit at risk</b> is what the books claim that 2B does not support:
/// the claimable tax of every bill missing from 2B, the claimable tax a
/// mismatched bill holds above 2B's figure, and a matched bill's claimable
/// tax where 2B marks the credit unavailable.
/// </summary>
public static class Gstr2bMatcherRepository
{
    public const decimal Tolerance = 1m;

    /// <param name="candidates">Bills from the suppliers in the file, any date: a 2B invoice may be billed in another month.</param>
    /// <param name="period">Bills whose supplier invoice falls in the 2B month: the ones 2B should mention.</param>
    public static Gstr2bMatchResult Match(
        IReadOnlyList<Gstr2bInvoice> invoices,
        IReadOnlyList<BooksBill> candidates,
        IReadOnlyList<BooksBill> period)
    {
        List<Gstr2bLine> lines = [];
        HashSet<long> used = [];
        decimal atRisk = 0m;

        ILookup<(string, string), BooksBill> exact = candidates.ToLookup(b => (b.Gstin.ToUpperInvariant(), b.SupplierInvoiceNo.Trim()));
        ILookup<(string, string), BooksBill> loose = candidates.ToLookup(b => (b.Gstin.ToUpperInvariant(), Normalise(b.SupplierInvoiceNo)));

        foreach (Gstr2bInvoice invoice in invoices)
        {
            string gstin = invoice.SupplierGstin.ToUpperInvariant();
            BooksBill? bill = exact[(gstin, invoice.InvoiceNo.Trim())].FirstOrDefault(b => !used.Contains(b.BillId));
            bool loosely = false;

            if (bill is null)
            {
                bill = loose[(gstin, Normalise(invoice.InvoiceNo))].FirstOrDefault(b => !used.Contains(b.BillId));
                loosely = bill is not null;
            }

            Gstr2bLine line = new()
            {
                SupplierGstin = gstin,
                SupplierName = invoice.SupplierName,
                InvoiceNo = invoice.InvoiceNo,
                InvoiceDate = invoice.InvoiceDate,
                ReverseCharge = invoice.ReverseCharge,
                ItcAvailable = invoice.ItcAvailable,
                TwoBTaxable = invoice.Taxable,
                TwoBTax = invoice.Tax,
            };

            if (bill is null)
            {
                line.MatchState = Gstr2bMatchState.MissingInBooks;
                line.Note = "No posted bill records this invoice.";
                lines.Add(line);
                continue;
            }

            used.Add(bill.BillId);
            line.BillId = bill.BillId;
            line.BillNo = bill.BillNo;
            line.BooksTaxable = bill.Taxable;
            line.BooksTax = bill.Tax;

            List<string> notes = [];
            if (loosely)
            {
                notes.Add($"Matched to '{bill.SupplierInvoiceNo}' ignoring case, separators and leading zeros.");
            }

            bool dateDiffers = bill.SupplierInvoiceDate != invoice.InvoiceDate;
            decimal taxableGap = bill.Taxable - invoice.Taxable;
            decimal taxGap = bill.Tax - invoice.Tax;

            if (dateDiffers)
            {
                notes.Add($"Dated {invoice.InvoiceDate:dd-MM-yyyy} in 2B and {bill.SupplierInvoiceDate:dd-MM-yyyy} in the books.");
            }

            if (Math.Abs(taxableGap) > Tolerance)
            {
                notes.Add($"Taxable value differs by {taxableGap:N2} (books less 2B).");
            }

            if (Math.Abs(taxGap) > Tolerance)
            {
                notes.Add($"Tax differs by {taxGap:N2} (books less 2B).");
            }

            line.MatchState = dateDiffers || Math.Abs(taxableGap) > Tolerance || Math.Abs(taxGap) > Tolerance
                ? Gstr2bMatchState.Mismatched
                : Gstr2bMatchState.Matched;

            if (line.MatchState == Gstr2bMatchState.Mismatched && taxGap > Tolerance)
            {
                atRisk += Math.Min(taxGap, bill.EligibleTax);
            }

            if (!invoice.ItcAvailable)
            {
                notes.Add("2B marks this credit as not available.");
                atRisk += bill.EligibleTax;
            }

            line.Note = notes.Count == 0 ? null : Truncate(string.Join(" ", notes));
            lines.Add(line);
        }

        foreach (BooksBill bill in period.Where(b => !used.Contains(b.BillId)).OrderBy(b => b.SupplierInvoiceDate).ThenBy(b => b.BillNo, StringComparer.Ordinal))
        {
            used.Add(bill.BillId);
            atRisk += bill.EligibleTax;

            lines.Add(new Gstr2bLine
            {
                MatchState = Gstr2bMatchState.MissingIn2b,
                SupplierGstin = bill.Gstin.ToUpperInvariant(),
                InvoiceNo = bill.SupplierInvoiceNo,
                InvoiceDate = bill.SupplierInvoiceDate,
                ReverseCharge = bill.ReverseCharge,
                BooksTaxable = bill.Taxable,
                BooksTax = bill.Tax,
                BillId = bill.BillId,
                BillNo = bill.BillNo,
                Note = "The supplier has not reported this invoice.",
            });
        }

        return new Gstr2bMatchResult(lines, Math.Round(atRisk, 2));
    }

    /// <summary>
    /// An invoice number with case, separators and leading zeros ignored:
    /// letters and digits only, each run of digits without its leading zeros.
    /// "INV/0007-A" and "inv 7a" are both "INV7A".
    /// </summary>
    public static string Normalise(string number)
    {
        StringBuilder result = new();
        StringBuilder digits = new();

        void FlushDigits()
        {
            if (digits.Length == 0)
            {
                return;
            }

            string trimmed = digits.ToString().TrimStart('0');
            result.Append(trimmed.Length == 0 ? "0" : trimmed);
            digits.Clear();
        }

        foreach (char c in number.ToUpperInvariant())
        {
            if (char.IsAsciiDigit(c))
            {
                digits.Append(c);
            }
            else
            {
                FlushDigits();
                if (char.IsAsciiLetter(c))
                {
                    result.Append(c);
                }
            }
        }

        FlushDigits();
        return result.ToString();
    }

    private static string Truncate(string text) => text.Length <= 300 ? text : text[..300];
}
