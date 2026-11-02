using Accounting.Entity.Enums;
using Shared.Kernel.Printing;
using Shared.Kernel.Tenancy;

namespace Accounting.Repository.Repositories;

/// <summary>A receipt's or payment's header, as the print template reads it.</summary>
public sealed record MoneyPrintHeader(
    string? TransactionNo,
    DateOnly TransactionDate,
    MoneyDocumentStatus Status,
    decimal Amount,
    string CurrencyCode,
    decimal ExchangeRate,
    PaymentMethod PaymentMethod,
    string? ReferenceNo,
    string? Memo);

/// <summary>
/// One line of a receipt or payment: what the money was applied to. The mapped document's number, date and
/// total are null when the line settles no document (an advance) or the ledger holds none for it.
/// </summary>
public sealed record MoneyPrintLine(
    int LineNumber,
    int LedgerSourceId,
    string? DocumentNo,
    DateOnly? DocumentDate,
    decimal? DocumentTotal,
    decimal Amount,
    string? LineMemo);

/// <summary>
/// A receipt (<c>RCM</c>) or a payment (<c>SPM</c>) as the print template reads it, keyed by the tags
/// <see cref="PlaceholderCatalog"/> names. One builder serves both: they are the same document in opposite
/// directions, with the same lines (what the money was applied to) and the same totals.
///
/// <b>Values, not formatted text</b>: amounts go out as decimals and dates as dates, and the template's masks
/// format them in Printing. Pure, so what a receipt prints as can be tested without a service or a database.
///
/// <b>Not filled, and why</b> — each prints as nothing rather than as a guess: <c>Alloc.Balance</c> (what the
/// document still owes after this and every other allocation is the settlement read's answer, not this
/// document's), the party's address and GSTIN (they live with the contact, not on the receipt),
/// <c>Totals.AmountInWords</c> and the tax block (a receipt carries no tax).
/// </summary>
public static class MoneyPrintPayloadRepository
{
    public static PrintPayload Build(
        MoneyPrintHeader header,
        IReadOnlyList<MoneyPrintLine> lines,
        OrgIdentity organization,
        string partyName,
        string? bankAccountName)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(organization);

        var payload = new PrintPayload();
        var singles = payload.Singles;

        DocumentPrintPayload.AddOrganization(payload, organization);

        // A draft holds no number: it is taken from the series when the document posts.
        singles["Document.No"] = header.TransactionNo;
        singles["Document.Date"] = header.TransactionDate;
        singles["Document.Status"] = header.Status.ToString();
        singles["Document.Currency"] = header.CurrencyCode;
        singles["Document.ExchangeRate"] = header.ExchangeRate;
        singles["Document.Notes"] = PrintText.Lines(header.Memo);
        singles["Document.Reference"] = header.ReferenceNo;
        singles["Document.PaymentMode"] = PaymentLabel(header.PaymentMethod);
        singles["Document.BankAccountName"] = bankAccountName;

        singles["Party.Name"] = partyName;

        // What moved is the one figure a receipt has. It is both the sub-total and the total, with nothing to
        // round, so the generic footer reads the same as on every other document.
        singles["Totals.SubTotal"] = header.Amount;
        singles["Totals.RoundOff"] = 0m;
        singles["Totals.GrandTotal"] = header.Amount;

        payload.Lists["Alloc"] = [.. lines
            .OrderBy(l => l.LineNumber)
            .Select(line => (IReadOnlyDictionary<string, object?>)
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    // A line that settles no document says what it is instead: an advance, an overpayment.
                    ["Alloc.DocumentNo"] = line.DocumentNo ?? SourceLabel(line.LedgerSourceId),
                    ["Alloc.DocumentDate"] = line.DocumentDate,
                    ["Alloc.DocumentTotal"] = line.DocumentTotal,
                    ["Alloc.Amount"] = line.Amount,
                })];

        return payload;
    }

    /// <summary>What a line's ledger source means, as the other party reads it. Mirrors the screens' own table.</summary>
    public static string SourceLabel(int ledgerSourceId) => ledgerSourceId switch
    {
        2 => "Bill payment",
        3 => "Invoice payment",
        4 => "Refund of our overpayment",
        6 => "Credit note refund",
        7 => "Debit note refund",
        8 => "Advance to supplier",
        9 => "Advance from customer",
        16 => "Overpayment to supplier",
        17 => "Overpayment from customer",
        18 => "Refund of overpayment",
        19 => "Refund of advance",
        20 => "Refund of our advance",
        _ => "Payment",
    };

    public static string PaymentLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Cash",
        PaymentMethod.Cheque => "Cheque",
        PaymentMethod.BankTransfer => "Bank transfer",
        PaymentMethod.Upi => "UPI",
        PaymentMethod.Card => "Card",
        PaymentMethod.DemandDraft => "Demand draft",
        PaymentMethod.Wallet => "Wallet",
        _ => "Other",
    };

    /// <summary>A posted document prints clean; a voided one says so; a draft is not final.</summary>
    public static string? Watermark(MoneyDocumentStatus status) => status switch
    {
        MoneyDocumentStatus.Posted => null,
        MoneyDocumentStatus.Void => "VOID",
        _ => "DRAFT",
    };
}
