using Banking.Entity.Models;

namespace Banking.Api.Services;

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
    public static LedgerPostingLeg Control(
        int lineNumber,
        int ledgerSourceId,
        MoneyLeg target,
        long contactId,
        decimal debit,
        decimal credit,
        string? memo) =>
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
            memo);

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
