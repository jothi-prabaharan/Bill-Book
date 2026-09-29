using Accounting.Entity.Enums;
using Accounting.Entity.TableEntities;

namespace Accounting.Repository.Repositories;

/// <summary>A receipt's withheld TDS, as the register holds it.</summary>
public sealed record BooksTds(long ReceiveMoneyId, string? DocumentNo, string? Tan, DateOnly Date, decimal Tds, string? SectionCode);

public sealed record Form26asMatchResult(IReadOnlyList<Form26asLine> Lines, decimal CreditAtRisk);

/// <summary>
/// Matches Form 26AS against the TDS receipts record customers withholding
/// (TK-117). Pure.
///
/// <list type="number">
/// <item>A statement transaction matches an unmatched receipt with the same
/// deductor TAN and the same TDS within a rupee, the nearest date first:
/// <see cref="Form26asMatchState.Matched"/>.</item>
/// <item>What is left is paired by TAN in date order and marked
/// <see cref="Form26asMatchState.AmountDiffers"/>, with the difference in the note.</item>
/// <item>A statement transaction with no receipt is
/// <see cref="Form26asMatchState.MissingInBooks"/>; a receipt the statement does
/// not show is <see cref="Form26asMatchState.MissingIn26as"/>.</item>
/// </list>
///
/// <b>The credit at risk</b> is TDS the books claim that 26AS does not show: a
/// receipt missing from it, and the part of a differing receipt above the
/// statement's figure. A receipt with no TAN recorded can only ever be missing.
/// </summary>
public static class Form26asMatcherRepository
{
    public const decimal Tolerance = 1m;

    public static Form26asMatchResult Match(IReadOnlyList<Form26asTransaction> statement, IReadOnlyList<BooksTds> books)
    {
        List<Form26asLine> lines = [];
        HashSet<long> used = [];
        List<Form26asTransaction> left = [];
        decimal atRisk = 0m;

        foreach (Form26asTransaction t in statement)
        {
            BooksTds? hit = books
                .Where(b => !used.Contains(b.ReceiveMoneyId)
                    && string.Equals(b.Tan, t.DeductorTan, StringComparison.OrdinalIgnoreCase)
                    && Math.Abs(b.Tds - t.Tds) <= Tolerance)
                .OrderBy(b => t.TransactionDate is DateOnly d ? Math.Abs(b.Date.DayNumber - d.DayNumber) : 0)
                .FirstOrDefault();

            if (hit is null)
            {
                left.Add(t);
                continue;
            }

            used.Add(hit.ReceiveMoneyId);
            lines.Add(Line(t, hit, Form26asMatchState.Matched, Status(t)));
        }

        foreach (Form26asTransaction t in left.OrderBy(t => t.TransactionDate))
        {
            BooksTds? pair = books
                .Where(b => !used.Contains(b.ReceiveMoneyId) && string.Equals(b.Tan, t.DeductorTan, StringComparison.OrdinalIgnoreCase))
                .OrderBy(b => b.Date)
                .FirstOrDefault();

            if (pair is null)
            {
                lines.Add(Line(t, null, Form26asMatchState.MissingInBooks,
                    Join("No receipt records this deductor withholding it.", Status(t))));
                continue;
            }

            used.Add(pair.ReceiveMoneyId);
            decimal gap = pair.Tds - t.Tds;
            atRisk += Math.Max(0m, gap);
            lines.Add(Line(t, pair, Form26asMatchState.AmountDiffers,
                Join($"TDS differs by {gap:N2} (books less 26AS).", Status(t))));
        }

        foreach (BooksTds b in books.Where(b => !used.Contains(b.ReceiveMoneyId)).OrderBy(b => b.Date))
        {
            atRisk += b.Tds;
            lines.Add(new Form26asLine
            {
                MatchState = Form26asMatchState.MissingIn26as,
                DeductorTan = b.Tan ?? "(no TAN)",
                SectionCode = b.SectionCode,
                TransactionDate = b.Date,
                BooksTds = b.Tds,
                ReceiveMoneyId = b.ReceiveMoneyId,
                DocumentNo = b.DocumentNo,
                Note = b.Tan is null
                    ? "The receipt records no TAN, so it cannot be found in 26AS. Add the customer's TAN."
                    : "The deductor has not reported this deduction.",
            });
        }

        return new Form26asMatchResult(lines, Math.Round(atRisk, 2));
    }

    private static Form26asLine Line(Form26asTransaction t, BooksTds? b, Form26asMatchState state, string? note) => new()
    {
        MatchState = state,
        DeductorTan = t.DeductorTan,
        DeductorName = t.DeductorName is { Length: > 200 } n ? n[..200] : t.DeductorName,
        SectionCode = t.SectionCode is { Length: > 10 } s ? s[..10] : t.SectionCode,
        TransactionDate = t.TransactionDate,
        StatementAmountPaid = t.AmountPaid,
        StatementTds = t.Tds,
        BooksTds = b?.Tds,
        ReceiveMoneyId = b?.ReceiveMoneyId,
        DocumentNo = b?.DocumentNo,
        Note = note,
    };

    /// <summary>A booking status other than F (final) is worth saying: the credit is not yet claimable.</summary>
    private static string? Status(Form26asTransaction t) =>
        t.BookingStatus is { Length: > 0 } status && !string.Equals(status, "F", StringComparison.OrdinalIgnoreCase)
            ? $"Booking status {status}: the deductor's deposit is not yet final."
            : null;

    private static string Join(string first, string? second) => second is null ? first : first + " " + second;
}
