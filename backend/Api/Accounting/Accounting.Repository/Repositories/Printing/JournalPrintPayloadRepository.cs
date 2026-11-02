using Accounting.Entity.Enums;
using Shared.Kernel.Printing;
using Shared.Kernel.Tenancy;

namespace Accounting.Repository.Repositories;

/// <summary>A journal's header, as the print template reads it.</summary>
public sealed record JournalPrintHeader(
    string? JournalNo,
    DateOnly JournalDate,
    JournalStatus Status,
    string CurrencyCode,
    decimal ExchangeRate,
    string? Reference,
    string? Memo);

/// <summary>One journal line: an account (and the sub-account under it, if any) and one money column.</summary>
public sealed record JournalPrintLine(
    int LineNumber,
    string AccountCode,
    string AccountName,
    string? SubAccountName,
    string? LineMemo,
    decimal Debit,
    decimal Credit);

/// <summary>
/// A manual journal (<c>JRN</c>) as the print template reads it, keyed by the tags
/// <see cref="PlaceholderCatalog"/> names: the account lines and the total they balance to. A journal has no
/// counterparty, so there is no party block.
///
/// <b>Values, not formatted text</b>, as every other document. Pure, so what a journal prints as can be tested
/// without a service or a database. A line shows the account and, beneath it, the sub-account when it has one
/// (a contact's receivable, an item's stock), because the account alone says "Accounts Receivable" and not
/// whose.
/// </summary>
public static class JournalPrintPayloadRepository
{
    public static PrintPayload Build(
        JournalPrintHeader header,
        IReadOnlyList<JournalPrintLine> lines,
        OrgIdentity organization)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(organization);

        var payload = new PrintPayload();
        var singles = payload.Singles;

        DocumentPrintPayload.AddOrganization(payload, organization);

        // A draft holds no number: it is taken from the series when the journal posts.
        singles["Document.No"] = header.JournalNo;
        singles["Document.Date"] = header.JournalDate;
        singles["Document.Status"] = header.Status.ToString();
        singles["Document.Currency"] = header.CurrencyCode;
        singles["Document.ExchangeRate"] = header.ExchangeRate;
        singles["Document.Reference"] = header.Reference;
        singles["Document.Notes"] = PrintText.Lines(header.Memo);

        // A journal balances, so either column is the total; a draft may not, and then the debit side is
        // what it says it moves.
        decimal total = lines.Sum(l => l.Debit);
        singles["Totals.SubTotal"] = total;
        singles["Totals.RoundOff"] = 0m;
        singles["Totals.GrandTotal"] = total;

        payload.Lists["Line"] = [.. lines
            .OrderBy(l => l.LineNumber)
            .Select(line => (IReadOnlyDictionary<string, object?>)
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Line.AccountCode"] = line.AccountCode,
                    ["Line.AccountName"] = line.SubAccountName is { Length: > 0 } sub
                        ? $"{line.AccountName} — {sub}"
                        : line.AccountName,
                    ["Line.Description"] = line.LineMemo,
                    // A zero column prints as nothing, as a ledger does: the money is on one side of a line.
                    ["Line.Debit"] = line.Debit == 0m ? null : line.Debit,
                    ["Line.Credit"] = line.Credit == 0m ? null : line.Credit,
                })];

        return payload;
    }

    /// <summary>
    /// A posted journal prints clean. A draft is not final, and a reversed one stands but has been offset in
    /// full by another, which a copy handed to an auditor must say.
    /// </summary>
    public static string? Watermark(JournalStatus status) => status switch
    {
        JournalStatus.Posted => null,
        JournalStatus.Reversed => "REVERSED",
        _ => "DRAFT",
    };
}
