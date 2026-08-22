using Accounting.Entity.Models;

namespace Accounting.Api.Services;

/// <summary>
/// What a money document's lines are allowed to say about the documents they
/// settle.
///
/// <b>One payment, several documents, one line each.</b> That much T6.2 already
/// gave: the ledger source and the settled document both sit on the line, so
/// ₹50,000 across three bills is three lines and three pairs of ledger legs, each
/// traceable to the bill it cleared. What was missing is that nothing checked the
/// lines were saying anything sensible — and the two mistakes available here are
/// both silent.
///
/// <b>A line naming the wrong kind of document</b> traces a payment to something
/// it did not pay. Worse than a missing link, because a statement reconciling on
/// that link reads as answered.
///
/// <b>Two lines naming the same document</b> leave two ledger rows against one
/// balance. Splitting a payment across two lines of one bill is not partial
/// payment — partial payment is one line for less than the bill — it is a
/// keying slip that no report can undo afterwards.
///
/// <b>What is deliberately not here: how much a document actually owes.</b> That
/// is T5.1, and it needs the bill and the invoice to exist. Until then a line may
/// settle more than its document is for; this refuses the mistakes that can be
/// caught without asking anyone what the balance is.
/// </summary>
public static class MoneyAllocation
{
    /// <summary>
    /// Checks every line's settled document, or returns null when they all pass.
    /// </summary>
    /// <param name="spending">
    /// Which side's source map applies. Spend and receive share this check but not
    /// their catalogue of sources.
    /// </param>
    public static MoneyDocumentResult? Check(
        IReadOnlyList<SaveMoneyLineRequest> lines, bool spending)
    {
        // Keyed on the source as well as the document, because paying a bill and
        // overpaying against it are two different claims on one bill — which is
        // the whole of T6.2's overpayment. Two *bill payment* lines against one
        // bill is the keying slip; a bill payment and its excess are not.
        var claimed = new HashSet<(int, string, long)>();

        for (int index = 0; index < lines.Count; index++)
        {
            SaveMoneyLineRequest line = lines[index];
            int lineNumber = index + 1;

            string? settleable = MoneyPostingMap.Settles(line.LedgerSourceId);
            bool named = line.MappingTransactionTypeCode is { Length: > 0 };

            // Half a mapping is refused by the database, and this reads the type
            // code alone deliberately: a type with no id is caught there with the
            // constraint's own message, and repeating it here would give the same
            // mistake two different explanations.
            if (!named)
            {
                continue;
            }

            string code = line.MappingTransactionTypeCode!.ToUpperInvariant();

            if (settleable is null)
            {
                return new MoneyDocumentResult(
                    MoneyDocumentOutcome.MappingNotSettleable, 0,
                    $"Line {lineNumber} settles nothing — an advance is placed before any "
                        + $"document exists — so it cannot name {code}.");
            }

            if (!string.Equals(code, settleable, StringComparison.Ordinal))
            {
                return new MoneyDocumentResult(
                    MoneyDocumentOutcome.MappingNotSettleable, 0,
                    $"Line {lineNumber} is for something that settles a {settleable}, "
                        + $"so it cannot name a {code}.");
            }

            if (line.MappingTransactionId is long id
                && !claimed.Add((line.LedgerSourceId, code, id)))
            {
                return new MoneyDocumentResult(
                    MoneyDocumentOutcome.MappingRepeated, 0,
                    $"Line {lineNumber} settles {code} {id} the same way an earlier line does. "
                        + $"A payment claims each document once per purpose — put the whole "
                        + $"amount for {code} {id} on one line.");
            }
        }

        return null;
    }

    /// <summary>
    /// What the header settles, given its lines. One document when every line
    /// that names one names the same, and nothing at all otherwise — a payment
    /// across three bills is about three, and picking one of them for the header
    /// would be picking arbitrarily.
    ///
    /// <b>Derived rather than taken from the caller.</b> The header's mapping is a
    /// summary of the lines, not a fact beside them: a screen that sent one while
    /// the lines said another would leave a document whose header and body
    /// disagree, and the header is what a list reads first.
    /// </summary>
    public static (string? TypeCode, long? TransactionId) HeaderMapping(
        IReadOnlyList<SaveMoneyLineRequest> lines)
    {
        (string, long)[] named = [.. lines
            .Where(l => l.MappingTransactionTypeCode is { Length: > 0 }
                && l.MappingTransactionId is not null)
            .Select(l => (l.MappingTransactionTypeCode!.ToUpperInvariant(), l.MappingTransactionId!.Value))
            .Distinct()];

        return named.Length == 1 ? (named[0].Item1, named[0].Item2) : (null, null);
    }
}
