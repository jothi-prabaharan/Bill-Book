using Accounting.Entity.Enums;
using Accounting.Entity.Models;

namespace Accounting.Api.Services;

/// <summary>
/// The parts of posting a money document that are identical whichever direction
/// the money runs: the guards before it, the legs it produces, and the base
/// currency conversion.
///
/// Spend and receive are mirrors, and a mirror written twice drifts. What differs
/// between them is which side of each pair is the debit, and that is the caller's
/// two arguments rather than a second copy of this.
/// </summary>
public static class MoneyPosting
{
    /// <summary>
    /// Everything that must hold before a document may reach the ledger: it has
    /// lines, they add up, and the books are open on its date.
    ///
    /// Returns null when the document may post. The allocation check is here as
    /// well as in the database because the trigger reports a constraint and this
    /// reports the difference.
    /// </summary>
    public static async Task<MoneyDocumentResult?> CheckPostableAsync(
        IAccountingLedger ledger,
        DateOnly transactionDate,
        decimal headerAmount,
        decimal allocated,
        int lineCount,
        CancellationToken ct)
    {
        if (lineCount == 0)
        {
            return new MoneyDocumentResult(
                MoneyDocumentOutcome.NoLines, 0,
                "There is nothing to post — the document has no lines.");
        }

        if (allocated != headerAmount)
        {
            return new MoneyDocumentResult(
                MoneyDocumentOutcome.NotAllocated, 0,
                $"The lines come to {allocated:0.00} but the document is for "
                    + $"{headerAmount:0.00}. It is out by {Math.Abs(headerAmount - allocated):0.00}.");
        }

        return await CheckPeriodAsync(ledger, transactionDate, ct);
    }

    /// <summary>
    /// Whether the books are open on this date for this caller.
    ///
    /// <b>Unreadable is not unlocked.</b> If Accounting cannot say, the document
    /// is refused as transient rather than allowed through — a lock that fails
    /// open because a lookup blipped is not a lock.
    /// </summary>
    public static async Task<MoneyDocumentResult?> CheckPeriodAsync(
        IAccountingLedger ledger, DateOnly transactionDate, CancellationToken ct)
    {
        try
        {
            if (await ledger.LockedUptoAsync(ct) is DateOnly closed && transactionDate <= closed)
            {
                return new MoneyDocumentResult(
                    MoneyDocumentOutcome.PeriodClosed, 0,
                    $"The books are closed to you up to {closed:dd MMM yyyy}. "
                        + "This document is dated on or before that.");
            }
        }
        catch (PeriodLockUnavailableException)
        {
            return new MoneyDocumentResult(
                MoneyDocumentOutcome.PeriodLockUnavailable, 0,
                "Whether the books are closed could not be established, so nothing was posted. "
                    + "This can be retried.");
        }

        return null;
    }

    /// <summary>
    /// The control leg — the contact's side of the movement. Named by system
    /// name, because an account id is a per-branch number Banking does not own.
    /// </summary>
    /// <param name="settledRate">
    /// The rate the balance being relieved was booked at, when that is not the
    /// rate the payment moved at. The control leg comes off at it so the contact
    /// is left owing nothing rather than a residue in a currency they have
    /// already settled; <see cref="Fx"/> carries what the two rates differ by.
    /// </param>
    public static LedgerPostingLeg Control(
        int lineNumber,
        int ledgerSourceId,
        MoneyLeg target,
        long contactId,
        decimal debit,
        decimal credit,
        string? memo,
        decimal? settledRate = null) =>
        new(
            MoneyPostingMap.ControlLedgerType,
            ledgerSourceId,
            lineNumber,
            target.AccountSystemName,
            AccountId: null,

            // 1 is Accounting's SubAccountReferenceType.Contact. Sent as a number
            // for the same reason the purpose is — Banking does not reference
            // Accounting's assemblies.
            SubAccountReferenceType: 1,
            contactId,
            target.SubAccountPurpose,
            debit,
            credit,
            memo,

            // The currency is unchanged — the balance was always in it. Only the
            // rate differs, which is the whole of what an exchange difference is.
            CurrencyCode: null,
            ExchangeRate: settledRate);

    /// <summary>
    /// The bank leg. <b>The one place Banking names an account by id</b>, and
    /// legitimately: Accounting issued that id when the bank account was created,
    /// and a bank account's GL account has no system name to resolve it by.
    /// </summary>
    public static LedgerPostingLeg Bank(
        int lineNumber,
        int ledgerSourceId,
        long ledgerAccountId,
        decimal debit,
        decimal credit,
        string? memo) =>
        new(
            MoneyPostingMap.ControlLedgerType,
            ledgerSourceId,
            lineNumber,
            AccountSystemName: null,
            ledgerAccountId,
            SubAccountReferenceType: null,
            SubAccountReferenceId: null,
            MoneyPostingMap.Primary,
            debit,
            credit,
            memo);

    /// <summary>
    /// The exchange difference on one settled line, or null when there is none.
    ///
    /// <b>It is whatever is left over, not a formula per direction.</b> The
    /// balance comes off at the rate it went on at and the bank moves at the rate
    /// it moved at; those two base amounts do not agree, and the gap is the gain
    /// or the loss. So the leg simply takes the side the pair is short on — which
    /// is why this is direction-free, and why it cannot be wrong about the sign
    /// the way a per-direction rule can.
    ///
    /// A loss debits Realized FX Gain/Loss and a gain credits it. The account is
    /// Income: seeded once per branch, so a gain and a loss are one line that nets
    /// rather than two that have to be read together.
    ///
    /// <b>Denominated in base, at rate 1.</b> The difference never existed in the
    /// foreign currency — it is the consequence of two rates, not an amount anyone
    /// paid — so recording it in one would invent a figure the counterparty never
    /// saw.
    /// </summary>
    /// <param name="baseCurrency">The branch's own currency. What the difference is in.</param>
    public static LedgerPostingLeg? Fx(
        int lineNumber,
        int ledgerSourceId,
        string baseCurrency,
        decimal controlDebitBase,
        decimal controlCreditBase,
        decimal bankDebitBase,
        decimal bankCreditBase,
        string? memo)
    {
        decimal shortfall =
            controlDebitBase + bankDebitBase - controlCreditBase - bankCreditBase;

        if (shortfall == 0m)
        {
            return null;
        }

        return new LedgerPostingLeg(
            FxLedgerType,
            ledgerSourceId,
            lineNumber,
            SystemAccountNames.Of(SystemAccount.RealizedFxGainLoss),
            AccountId: null,
            SubAccountReferenceType: null,
            SubAccountReferenceId: null,
            MoneyPostingMap.Primary,

            // Short on debits means the credits ran ahead: more base left the
            // bank than the balance was carrying, which is a loss.
            DebitAmount: shortfall < 0 ? -shortfall : 0m,
            CreditAmount: shortfall > 0 ? shortfall : 0m,
            memo,
            baseCurrency,
            ExchangeRate: 1m);
    }

    /// <summary><c>mst.LedgerTypes</c> 5 — FX, the realized exchange gain or loss.</summary>
    public const int FxLedgerType = 5;

    /// <summary>
    /// The base-currency amount. Two decimals, away from zero — the same rule the
    /// ledger uses, because the two have to agree to the paisa or the balance
    /// trigger refuses the posting.
    /// </summary>
    public static decimal Base(decimal amount, decimal rate) =>
        rate == 1m ? amount : Math.Round(amount / rate, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Shapes a header and its lines into the view the screens read.</summary>
public static class MoneyDocumentMapping
{
    public static MoneyDocumentView WithLines(
        MoneyDocumentListItem header, List<MoneyLineView> lines) => new()
    {
        DocumentId = header.DocumentId,
        TransactionNo = header.TransactionNo,
        TransactionDate = header.TransactionDate,
        BankAccountId = header.BankAccountId,
        BankAccountName = header.BankAccountName,
        ContactId = header.ContactId,
        ToBankAccountId = header.ToBankAccountId,
        ToBankAccountName = header.ToBankAccountName,
        Amount = header.Amount,
        CurrencyCode = header.CurrencyCode,
        ExchangeRate = header.ExchangeRate,
        PaymentMethod = header.PaymentMethod,
        ReferenceNo = header.ReferenceNo,
        Memo = header.Memo,
        Status = header.Status,
        PostedAt = header.PostedAt,
        MappingTransactionTypeCode = header.MappingTransactionTypeCode,
        MappingTransactionId = header.MappingTransactionId,
        Lines = lines,
    };
}
