using Banking.Api.Services;
using Banking.Entity.Models;
using Banking.Entity.TableEntities;
using Xunit;

namespace Banking.Api.Tests;

/// <summary>
/// Which document a statement line is the bank's version of.
///
/// <b>Every test here is really about one question: when is it safe to decide
/// without asking?</b> A wrong auto-match is worse than no match — it is recorded
/// as a reconciliation, it says the computer was confident, and the person who
/// would have caught it never sees the line. So the bar is high and these are the
/// cases that set it.
/// </summary>
public class StatementMatcherTests
{
    /// <summary>
    /// Amount, direction and reference all agreeing, with nothing else close. The
    /// one case confident enough to take without a person.
    /// </summary>
    [Fact]
    public void A_lone_candidate_whose_reference_agrees_is_confident()
    {
        List<MatchCandidateView> ranked = StatementMatcher.Rank(
            Line(withdrawal: 11_000m, date: new DateOnly(2026, 4, 3), reference: "N0412345"),
            [Spend(1, 11_000m, new DateOnly(2026, 4, 1), "N0412345")]);

        MatchCandidateView best = Assert.Single(ranked);

        Assert.True(best.IsConfident);
        Assert.Equal("SPM", best.TransactionTypeCode);
    }

    /// <summary>
    /// The case that decides the whole design. A business paying one supplier the
    /// same amount every week has several identical documents within days of each
    /// other — and picking between them is a coin toss that would be filed as a
    /// decision.
    /// </summary>
    [Fact]
    public void Two_equally_good_candidates_are_offered_rather_than_guessed_between()
    {
        List<MatchCandidateView> ranked = StatementMatcher.Rank(
            Line(withdrawal: 5_000m, date: new DateOnly(2026, 4, 10), reference: null),
            [
                Spend(1, 5_000m, new DateOnly(2026, 4, 10), null),
                Spend(2, 5_000m, new DateOnly(2026, 4, 10), null),
            ]);

        Assert.Equal(2, ranked.Count);
        Assert.All(ranked, c => Assert.False(c.IsConfident));
    }

    /// <summary>
    /// Amount and date agreeing, with no reference, is a coincidence a busy month
    /// produces several times over. Offered, ranked, and explicitly not confident.
    /// </summary>
    [Fact]
    public void Amount_and_date_alone_are_not_enough_to_decide()
    {
        MatchCandidateView only = Assert.Single(StatementMatcher.Rank(
            Line(withdrawal: 5_000m, date: new DateOnly(2026, 4, 10), reference: null),
            [Spend(1, 5_000m, new DateOnly(2026, 4, 10), null)]));

        Assert.False(only.IsConfident);
        Assert.Contains("Nothing else does", only.Reason);
    }

    /// <summary>
    /// A receipt is never a candidate for a withdrawal. Half the candidates
    /// disappear on direction alone, and a matcher that skipped this would offer
    /// money coming in as an explanation for money going out.
    /// </summary>
    [Fact]
    public void A_document_running_the_other_way_is_never_a_candidate()
    {
        Assert.Empty(StatementMatcher.Rank(
            Line(withdrawal: 5_000m, date: new DateOnly(2026, 4, 10), reference: "R1"),
            [Receive(1, 5_000m, new DateOnly(2026, 4, 10), "R1")]));
    }

    /// <summary>
    /// A cheque written on Monday clears on Thursday; a weekend adds two more.
    /// Ten days covers it, and beyond that the document is not offered at all.
    /// </summary>
    [Fact]
    public void A_document_beyond_the_date_window_is_not_offered()
    {
        Assert.Empty(StatementMatcher.Rank(
            Line(withdrawal: 5_000m, date: new DateOnly(2026, 4, 20), reference: "N1"),
            [Spend(1, 5_000m, new DateOnly(2026, 4, 1), "N1")]));

        Assert.Single(StatementMatcher.Rank(
            Line(withdrawal: 5_000m, date: new DateOnly(2026, 4, 9), reference: "N1"),
            [Spend(1, 5_000m, new DateOnly(2026, 4, 1), "N1")]));
    }

    /// <summary>
    /// The reference as the bank actually prints it: buried in a narration, with
    /// the bank's own prefixes around it. Compared on letters and digits only,
    /// either way round.
    /// </summary>
    [Fact]
    public void A_reference_buried_in_the_narration_still_counts()
    {
        var line = new BankStatementLine
        {
            TransactionDate = new DateOnly(2026, 4, 3),
            WithdrawalAmount = 11_000m,
            Description = "CHQ PAID - MICR CTS - 000123456",
            RowHash = "x",
        };

        MatchCandidateView best = Assert.Single(
            StatementMatcher.Rank(line, [Spend(1, 11_000m, new DateOnly(2026, 4, 1), "123456")]));

        Assert.True(best.IsConfident);
    }

    /// <summary>
    /// A reference too short to be evidence. "12" is inside almost any narration
    /// by accident, and a match made on it would be noise recorded as certainty.
    /// </summary>
    [Fact]
    public void A_reference_too_short_to_be_evidence_does_not_make_a_match_confident()
    {
        MatchCandidateView only = Assert.Single(StatementMatcher.Rank(
            Line(withdrawal: 5_000m, date: new DateOnly(2026, 4, 10), reference: "12"),
            [Spend(1, 5_000m, new DateOnly(2026, 4, 10), "12")]));

        Assert.False(only.IsConfident);
    }

    /// <summary>
    /// Amounts have to be exactly equal. A statement showing a bank charge taken
    /// out of a transfer is a different movement, not a near miss to be absorbed.
    /// </summary>
    [Fact]
    public void An_amount_that_is_merely_close_is_not_a_candidate()
    {
        Assert.Empty(StatementMatcher.Rank(
            Line(withdrawal: 5_000m, date: new DateOnly(2026, 4, 10), reference: "N1"),
            [Spend(1, 4_995m, new DateOnly(2026, 4, 10), "N1")]));
    }

    /// <summary>
    /// The nearer document wins when neither reference agrees — but neither is
    /// confident, so the ordering is a suggestion rather than a decision.
    /// </summary>
    [Fact]
    public void The_nearer_document_ranks_first_without_becoming_confident()
    {
        List<MatchCandidateView> ranked = StatementMatcher.Rank(
            Line(withdrawal: 5_000m, date: new DateOnly(2026, 4, 10), reference: null),
            [
                Spend(1, 5_000m, new DateOnly(2026, 4, 4), null),
                Spend(2, 5_000m, new DateOnly(2026, 4, 9), null),
            ]);

        Assert.Equal(2, ranked[0].TransactionId);
        Assert.False(ranked[0].IsConfident);
    }

    private static BankStatementLine Line(decimal withdrawal, DateOnly date, string? reference) =>
        new()
        {
            TransactionDate = date,
            WithdrawalAmount = withdrawal,
            ReferenceNo = reference,
            RowHash = "x",
        };

    private static MoneyDocumentCandidate Spend(
        long id, decimal amount, DateOnly date, string? reference) =>
        new("SPM", id, $"SPM/2026/{id:00000}", date, amount, true, 7, reference);

    private static MoneyDocumentCandidate Receive(
        long id, decimal amount, DateOnly date, string? reference) =>
        new("RCM", id, $"RCM/2026/{id:00000}", date, amount, false, 7, reference);
}
