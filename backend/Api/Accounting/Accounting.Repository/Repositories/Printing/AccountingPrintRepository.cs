using Accounting.Entity.Enums;
using Accounting.Entity.TableEntities;
using Accounting.Repository.Clients;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Documents;
using Shared.Kernel.Printing;
using Shared.Kernel.Tenancy;

namespace Accounting.Repository.Repositories;

/// <summary>
/// Lays out a receipt, a payment or a manual journal through its print template: reads the document from
/// Accounting's own tables, builds its payload, and has Printing render it.
///
/// Accounting never reads a template and Printing never reads a receipt, the boundary the Printing design was
/// argued on. Each method answers null when the document is not in this branch, which the caller answers as
/// not found; row-level security means another branch's id reads as none rather than as forbidden.
/// </summary>
public sealed class AccountingPrintRepository
{
    private readonly AccountingDbContext _db;
    private readonly IOrgIdentityProvider _orgIdentity;
    private readonly IContactNameLookup _contactNames;
    private readonly IPrintingClient _printing;

    public AccountingPrintRepository(
        AccountingDbContext db,
        IOrgIdentityProvider orgIdentity,
        IContactNameLookup contactNames,
        IPrintingClient printing)
    {
        _db = db;
        _orgIdentity = orgIdentity;
        _contactNames = contactNames;
        _printing = printing;
    }

    public async Task<PrintedDocument?> PrintReceiptAsync(long receiveMoneyId, CancellationToken ct)
    {
        ReceiveMoney? receipt = await _db.ReceiveMoney.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ReceiveMoneyId == receiveMoneyId, ct);

        if (receipt is null)
        {
            return null;
        }

        var details = await _db.ReceiveMoneyDetails.AsNoTracking()
            .Where(d => d.ReceiveMoneyId == receiveMoneyId)
            .Select(d => new MoneyDetail(d.LineNumber, d.LedgerSourceId, d.MappingTransactionTypeCode, d.MappingTransactionId, d.Amount, d.LineMemo))
            .ToListAsync(ct);

        return await RenderMoneyAsync(
            "RCM",
            new MoneyPrintHeader(
                receipt.TransactionNo, receipt.TransactionDate, receipt.Status, receipt.Amount,
                receipt.CurrencyCode, receipt.ExchangeRate, receipt.PaymentMethod, receipt.ReferenceNo, receipt.Memo),
            receipt.ContactId,
            receipt.BankAccountId,
            details,
            receipt.PrintTemplateId,
            ct);
    }

    public async Task<PrintedDocument?> PrintPaymentAsync(long spendMoneyId, CancellationToken ct)
    {
        SpendMoney? payment = await _db.SpendMoney.AsNoTracking()
            .FirstOrDefaultAsync(p => p.SpendMoneyId == spendMoneyId, ct);

        if (payment is null)
        {
            return null;
        }

        var details = await _db.SpendMoneyDetails.AsNoTracking()
            .Where(d => d.SpendMoneyId == spendMoneyId)
            .Select(d => new MoneyDetail(d.LineNumber, d.LedgerSourceId, d.MappingTransactionTypeCode, d.MappingTransactionId, d.Amount, d.LineMemo))
            .ToListAsync(ct);

        return await RenderMoneyAsync(
            "SPM",
            new MoneyPrintHeader(
                payment.TransactionNo, payment.TransactionDate, payment.Status, payment.Amount,
                payment.CurrencyCode, payment.ExchangeRate, payment.PaymentMethod, payment.ReferenceNo, payment.Memo),
            payment.ContactId,
            payment.BankAccountId,
            details,
            payment.PrintTemplateId,
            ct);
    }

    public async Task<PrintedDocument?> PrintJournalAsync(long journalId, CancellationToken ct)
    {
        Journal? journal = await _db.Journals.AsNoTracking()
            .FirstOrDefaultAsync(j => j.JournalId == journalId, ct);

        if (journal is null)
        {
            return null;
        }

        OrgIdentity organization = await OrganizationAsync(ct);

        // Each line's account, and the sub-account beneath it when it has one, in two reads rather than one
        // per line.
        List<JournalDetail> details = await _db.JournalDetails.AsNoTracking()
            .Where(d => d.JournalId == journalId)
            .OrderBy(d => d.LineNumber)
            .ToListAsync(ct);

        long[] accountIds = [.. details.Select(d => d.AccountId).Distinct()];
        Dictionary<long, (string Code, string Name)> accounts = await _db.Accounts.AsNoTracking()
            .Where(a => accountIds.Contains(a.AccountId))
            .ToDictionaryAsync(a => a.AccountId, a => (a.AccountCode, a.AccountName), ct);

        long[] subAccountIds = [.. details.Where(d => d.SubAccountId.HasValue).Select(d => d.SubAccountId!.Value).Distinct()];
        Dictionary<long, string> subAccounts = subAccountIds.Length == 0
            ? []
            : await _db.SubAccounts.AsNoTracking()
                .Where(s => subAccountIds.Contains(s.SubAccountId))
                .ToDictionaryAsync(s => s.SubAccountId, s => s.SubAccountName, ct);

        PrintPayload payload = JournalPrintPayloadRepository.Build(
            new JournalPrintHeader(
                journal.JournalNo, journal.JournalDate, journal.Status, journal.CurrencyCode,
                journal.ExchangeRate, journal.Reference, journal.Memo),
            [.. details.Select(d => new JournalPrintLine(
                d.LineNumber,
                accounts.TryGetValue(d.AccountId, out var account) ? account.Code : string.Empty,
                accounts.TryGetValue(d.AccountId, out account) ? account.Name : $"Account {d.AccountId}",
                d.SubAccountId is long subId && subAccounts.TryGetValue(subId, out string? subName) ? subName : null,
                d.LineMemo,
                d.DebitAmount,
                d.CreditAmount))],
            organization);

        return await RenderAsync(
            "JRN", journal.PrintTemplateId, payload,
            JournalPrintPayloadRepository.Watermark(journal.Status),
            journal.JournalNo, journal.Status.ToString(), ct);
    }

    private sealed record MoneyDetail(
        int LineNumber,
        int LedgerSourceId,
        string? MappingTransactionTypeCode,
        long? MappingTransactionId,
        decimal Amount,
        string? LineMemo);

    private async Task<PrintedDocument> RenderMoneyAsync(
        string typeCode,
        MoneyPrintHeader header,
        long contactId,
        long bankAccountId,
        List<MoneyDetail> details,
        long? printTemplateId,
        CancellationToken ct)
    {
        OrgIdentity organization = await OrganizationAsync(ct);

        var contacts = await _contactNames.ResolveAsync([contactId], ct);
        string partyName = contacts.TryGetValue(contactId, out var contact) ? contact.Name : string.Empty;

        string? bankAccountName = await _db.BankAccounts.AsNoTracking()
            .Where(b => b.BankAccountId == bankAccountId)
            .Select(b => b.AccountName)
            .FirstOrDefaultAsync(ct);

        Dictionary<(string Code, long Id), MappedDocument> mapped = await MappedDocumentsAsync(contactId, details, ct);

        PrintPayload payload = MoneyPrintPayloadRepository.Build(
            header,
            [.. details.Select(d =>
            {
                MappedDocument? document = d.MappingTransactionTypeCode is { Length: > 0 } code && d.MappingTransactionId is long id
                    ? (mapped.TryGetValue((code, id), out MappedDocument? found)
                        ? found
                        // The ledger holds nothing under it (a document type that carries no number there):
                        // name it the way the form does rather than print nothing.
                        : new MappedDocument($"{code} #{id}", null, null))
                    : null;

                return new MoneyPrintLine(
                    d.LineNumber, d.LedgerSourceId, document?.DocumentNo, document?.DocumentDate,
                    document?.DocumentTotal, d.Amount, d.LineMemo);
            })],
            organization,
            partyName,
            bankAccountName);

        return await RenderAsync(
            typeCode, printTemplateId, payload,
            MoneyPrintPayloadRepository.Watermark(header.Status),
            header.TransactionNo, header.Status.ToString(), ct);
    }

    private sealed record MappedDocument(string DocumentNo, DateOnly? DocumentDate, decimal? DocumentTotal);

    /// <summary>
    /// The number, date and total of each document a line was applied to, read from the ledger the way the
    /// allocation workspace reads them: the legs on the contact's control sub-accounts, whose net is what the
    /// document was posted for. The amounts are the document's own currency, so a figure beside a line in the
    /// same currency reads as it should.
    /// </summary>
    private async Task<Dictionary<(string Code, long Id), MappedDocument>> MappedDocumentsAsync(
        long contactId, List<MoneyDetail> details, CancellationToken ct)
    {
        List<(string Code, long Id)> keys = [.. details
            .Where(d => d.MappingTransactionTypeCode is { Length: > 0 } && d.MappingTransactionId is > 0)
            .Select(d => (d.MappingTransactionTypeCode!, d.MappingTransactionId!.Value))
            .Distinct()];

        if (keys.Count == 0)
        {
            return [];
        }

        List<string> codes = [.. keys.Select(k => k.Code).Distinct()];
        List<long> ids = [.. keys.Select(k => k.Id).Distinct()];

        var posted = await _db.JournalLedger.AsNoTracking()
            .Where(l => l.ContactId == contactId
                && l.LedgerTypeId == MoneyPostingMapRepository.ControlLedgerType
                && l.SubAccountId != null
                && codes.Contains(l.TransactionTypeCode)
                && ids.Contains(l.TransactionId))
            .GroupBy(l => new { l.TransactionTypeCode, l.TransactionId })
            .Select(g => new
            {
                g.Key.TransactionTypeCode,
                g.Key.TransactionId,
                Net = g.Sum(x => x.DebitAmount) - g.Sum(x => x.CreditAmount),
                DocumentDate = g.Min(x => x.LedgerDate),
                DocumentNo = g.Max(x => x.DocumentNo),
            })
            .ToListAsync(ct);

        Dictionary<(string Code, long Id), MappedDocument> found = [];
        foreach (var row in posted)
        {
            (string, long) key = (row.TransactionTypeCode, row.TransactionId);
            if (keys.Contains(key))
            {
                found[key] = new MappedDocument(
                    string.IsNullOrWhiteSpace(row.DocumentNo) ? $"{row.TransactionTypeCode} #{row.TransactionId}" : row.DocumentNo,
                    row.DocumentDate,
                    Math.Abs(row.Net));
            }
        }

        return found;
    }

    private async Task<OrgIdentity> OrganizationAsync(CancellationToken ct) =>
        // The issuer's name and GSTIN are on every document the branch hands over, so a branch that cannot be
        // identified stops the print rather than printing a placeholder name, as posting is stopped.
        await _orgIdentity.GetIdentityAsync(ct)
            ?? throw new InvalidOperationException(
                "The branch issuing this document could not be identified, so it cannot be printed.");

    private async Task<PrintedDocument> RenderAsync(
        string typeCode,
        long? printTemplateId,
        PrintPayload payload,
        string? watermark,
        string? documentNo,
        string status,
        CancellationToken ct)
    {
        PrintedDocument printed = await _printing.RenderAsync(typeCode, printTemplateId, payload, watermark, ct);

        // Which document this is, for the page that shows it (the view page's heading).
        printed.DocumentNo = documentNo;
        printed.ChosenPrintTemplateId = printTemplateId;
        printed.DocumentTypeCode = typeCode;
        printed.Status = status;
        return printed;
    }
}
