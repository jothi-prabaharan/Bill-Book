using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;

namespace Accounting.Api.Services;

/// <summary>
/// Works out which money document a statement line is the bank's version of.
///
/// <b>Matching is not posting, and this is where that matters most.</b> Tying a
/// line to a document writes nothing to the general ledger — the money already
/// moved and the document already posted it. What a match records is that the
/// bank and the books agree about one movement. A reconciliation that posted
/// would double every transaction it touched, which is the single worst thing
/// this feature could do.
///
/// <b>Auto-match only when there is exactly one plausible answer.</b> Two
/// candidates equally good is a line a person has to look at: picking one would
/// be a coin toss recorded as a decision, and the record would say the computer
/// was confident. So confidence requires both a strong signal and no rival.
/// </summary>
public static class StatementMatcher
{
    /// <summary>
    /// How far a document's date may sit from the bank's before it stops being a
    /// candidate at all.
    ///
    /// Generous on purpose. A cheque is written on Monday and clears on Thursday;
    /// a NEFT after banking hours lands the next working day; a weekend adds two.
    /// Ten days covers all of it. Narrowing this does not prevent wrong matches —
    /// the reference and the amount do that — it only hides the right one.
    /// </summary>
    private const int DateWindowDays = 10;

    /// <summary>
    /// Ranks the documents a line could be, best first. Empty when none is
    /// plausible, which is the honest answer for a bank charge nobody keyed.
    /// </summary>
    public static List<MatchCandidateView> Rank(
        BankStatementLine line, IReadOnlyList<MoneyDocumentCandidate> documents)
    {
        // A line is money out or money in, and only documents running the same
        // way can be it. Half the candidates disappear here, and a matcher that
        // skipped this would happily offer a receipt for a withdrawal.
        bool outward = line.WithdrawalAmount > 0;
        decimal amount = outward ? line.WithdrawalAmount : line.DepositAmount;

        var scored = new List<(MatchCandidateView View, int Score)>();

        foreach (MoneyDocumentCandidate document in documents)
        {
            if (document.IsOutward != outward || document.Amount != amount)
            {
                continue;
            }

            int days = Math.Abs(document.TransactionDate.DayNumber - line.TransactionDate.DayNumber);

            if (days > DateWindowDays)
            {
                continue;
            }

            bool referenceAgrees = ReferencesAgree(line, document);

            // The amount and the direction are already known to agree — that is
            // what got the document this far. What separates a confident match
            // from a plausible one is whether anything *else* agrees.
            int score = referenceAgrees ? 100 : 0;
            score += Math.Max(0, DateWindowDays - days);

            scored.Add((
                new MatchCandidateView
                {
                    TransactionTypeCode = document.TransactionTypeCode,
                    TransactionId = document.TransactionId,
                    TransactionNo = document.TransactionNo,
                    TransactionDate = document.TransactionDate,
                    Amount = document.Amount,
                    ContactId = document.ContactId,
                    ReferenceNo = document.ReferenceNo,
                    Reason = Describe(referenceAgrees, days),
                },
                score));
        }

        List<MatchCandidateView> ranked =
            [.. scored.OrderByDescending(s => s.Score).Select(s => s.View)];

        // Confident only when the best is genuinely better than everything else.
        // Two documents for the same amount within a few days of each other is
        // the ordinary case for a business paying the same supplier weekly, and
        // guessing between them is exactly what must not happen.
        if (scored.Count > 0)
        {
            int best = scored.Max(s => s.Score);
            bool alone = scored.Count(s => s.Score == best) == 1;

            // A reference agreeing is what makes a match safe without a person.
            // Amount and date alone are a coincidence a busy month produces
            // several times over.
            ranked[0].IsConfident = alone && best >= 100;
        }

        return ranked;
    }

    /// <summary>
    /// Whether the bank's reference and the document's are the same thing.
    ///
    /// Compared loosely, because they are never written the same way: a cheque
    /// keyed as <c>000123</c> appears on the statement inside
    /// <c>CHQ PAID - 123456 - 000123</c>, and a UTR is quoted with the bank's own
    /// prefix. Containment either way, on digits and letters only.
    /// </summary>
    private static bool ReferencesAgree(BankStatementLine line, MoneyDocumentCandidate document)
    {
        string ours = Squash(document.ReferenceNo);

        // Too short to be evidence. A two-character reference is inside almost
        // any narration by accident, and a match made on it would be noise
        // recorded as certainty.
        if (ours.Length < 4)
        {
            return false;
        }

        string theirs = Squash(line.ReferenceNo);
        string narration = Squash(line.Description);

        return (theirs.Length > 0 && (theirs.Contains(ours, StringComparison.Ordinal)
                || ours.Contains(theirs, StringComparison.Ordinal)))
            || narration.Contains(ours, StringComparison.Ordinal);
    }

    private static string Squash(string? text) =>
        text is not { Length: > 0 }
            ? string.Empty
            : new string([.. text.Where(char.IsLetterOrDigit)]).ToUpperInvariant();

    private static string Describe(bool referenceAgrees, int days)
    {
        string when = days == 0
            ? "same date"
            : days == 1 ? "1 day apart" : $"{days} days apart";

        return referenceAgrees
            ? $"Amount and reference both agree, {when}."
            : $"Amount agrees, {when}. Nothing else does — check before accepting.";
    }
}

/// <summary>
/// A posted money document, flattened so the matcher does not care which of the
/// three tables it came from.
/// </summary>
/// <param name="IsOutward">
/// Whether money left the account being reconciled. A transfer is outward from
/// the account it leaves and inward to the one it reaches, so the same document
/// appears twice with different answers — which is right: it shows on both
/// statements.
/// </param>
public sealed record MoneyDocumentCandidate(
    string TransactionTypeCode,
    long TransactionId,
    string? TransactionNo,
    DateOnly TransactionDate,
    decimal Amount,
    bool IsOutward,
    long? ContactId,
    string? ReferenceNo);
