using Banking.Entity.Models;

namespace Banking.Api.Services;

/// <summary>
/// Works out, for one money document, which of its lines settle a balance that
/// was booked at a different exchange rate from the one the payment is moving at.
///
/// <b>An invoice raised at one rate and settled at another does not clear
/// itself.</b> Relieve the receivable at today's rate and the contact is left
/// holding the difference — a balance in a currency they have already paid in
/// full, which nobody will think to look for because the document says settled.
/// So the balance comes off at the rate it went on at, the bank moves at the rate
/// it moved at, and the gap between the two is recognized as a realized gain or
/// loss on the day it is realized. That is the whole of T6.5.
///
/// <b>Every rate here was written down.</b> The payment's rate is a snapshot on
/// the payment; the document's rate is read back off that document's own ledger
/// rows. Nothing consults a rate table, because a historical document that
/// repriced itself would restate settled history.
/// </summary>
public static class MoneySettlement
{
    /// <summary>
    /// The rate each line's settled document was booked at, keyed by line number.
    /// A line is absent when it settles nothing, when the document has no postings
    /// yet, or when its rate is the payment's — all three meaning "no exchange
    /// difference to recognize".
    /// </summary>
    /// <param name="lines">
    /// (line number, settled type code, settled id) for each line of the document.
    /// </param>
    public static async Task<SettlementRatesResult> ResolveAsync(
        IAccountingLedger ledger,
        string documentCurrency,
        decimal documentRate,
        IEnumerable<(int LineNumber, string? TypeCode, long? TransactionId)> lines,
        CancellationToken ct)
    {
        var rates = new Dictionary<int, decimal>();

        // Cached per document, because a payment split across several lines of one
        // bill would otherwise ask the same question once per line.
        var seen = new Dictionary<(string, long), SettlementRate?>();

        foreach ((int lineNumber, string? typeCode, long? transactionId) in lines)
        {
            if (typeCode is not { Length: > 0 } code || transactionId is not long id)
            {
                continue;
            }

            var key = (code.ToUpperInvariant(), id);

            if (!seen.TryGetValue(key, out SettlementRate? settled))
            {
                try
                {
                    settled = await ledger.SettlementRateAsync(code, id, ct);
                }
                catch (SettlementRateUnavailableException)
                {
                    return new SettlementRatesResult(
                        new MoneyDocumentResult(
                            MoneyDocumentOutcome.SettlementRateUnavailable, 0,
                            "What the settled document was booked at could not be established, "
                                + "so nothing was posted. This can be retried."));
                }

                seen[key] = settled;
            }

            if (settled is null)
            {
                // Nothing posted against it. There is no earlier rate to differ
                // from, so there is no difference — not an error: a line may
                // legitimately name a document this branch has yet to post.
                continue;
            }

            if (!string.Equals(settled.CurrencyCode, documentCurrency, StringComparison.OrdinalIgnoreCase))
            {
                return new SettlementRatesResult(
                    new MoneyDocumentResult(
                        MoneyDocumentOutcome.SettlementCurrencyMismatch, 0,
                        $"Line {lineNumber} settles {code} {id}, which was raised in "
                            + $"{settled.CurrencyCode}, with a payment in {documentCurrency}. "
                            + "Pay it in the currency it was raised in."));
            }

            if (settled.ExchangeRate != documentRate)
            {
                rates[lineNumber] = settled.ExchangeRate;
            }
        }

        return new SettlementRatesResult(null, rates);
    }
}

/// <summary>
/// The rates, or the refusal that stopped them being established. Never both —
/// a refusal here means nothing may post, because posting the rest of the
/// document without it is exactly the silent residue this whole mechanism exists
/// to prevent.
/// </summary>
public sealed record SettlementRatesResult(
    MoneyDocumentResult? Refusal, IReadOnlyDictionary<int, decimal>? Rates = null)
{
    /// <summary>The rate line <paramref name="lineNumber"/> settles at, or null for the document's own.</summary>
    public decimal? For(int lineNumber) =>
        Rates is not null && Rates.TryGetValue(lineNumber, out decimal rate) ? rate : null;
}
