using Accounting.Entity.Enums;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Accounting.Repository.Clients;
using Accounting.Repository.Repositories;
using Shared.Kernel.Documents;
using Shared.Kernel.Printing;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// What a receipt, a payment and a journal print as, tested without a service or a database (TK-263).
/// Written by an AI and not run (section 0.5 of docs/Task_Details.md).
/// </summary>
public sealed class AccountingPrintPayloadTests
{
    private static readonly OrgIdentity Org = new(
        "Sample Traders", "33AABCU9603R1ZM", "12 Anna Salai", null, "Chennai", "33", "600002");

    private static MoneyPrintHeader Header(MoneyDocumentStatus status = MoneyDocumentStatus.Posted, string? number = "REC/2627/00001") => new(
        number, new DateOnly(2026, 11, 2), status, 4_000m, "INR", 1m, PaymentMethod.Upi, "UTR 998877", "Part payment");

    private static PrintPayload BuildMoney(params MoneyPrintLine[] lines) =>
        MoneyPrintPayloadRepository.Build(Header(), lines, Org, "Karnataka Traders", "HDFC Current");

    [Fact]
    public void A_receipt_carries_who_paid_how_and_into_which_account()
    {
        PrintPayload payload = BuildMoney();

        Assert.Equal("REC/2627/00001", payload.Singles["Document.No"]);
        Assert.Equal(new DateOnly(2026, 11, 2), payload.Singles["Document.Date"]);
        Assert.Equal("Posted", payload.Singles["Document.Status"]);
        Assert.Equal("Karnataka Traders", payload.Singles["Party.Name"]);
        Assert.Equal("UPI", payload.Singles["Document.PaymentMode"]);
        Assert.Equal("UTR 998877", payload.Singles["Document.Reference"]);
        Assert.Equal("HDFC Current", payload.Singles["Document.BankAccountName"]);
        Assert.Equal("Part payment", payload.Singles["Document.Notes"]);
        Assert.Equal("Sample Traders", payload.Singles["Organization.Name"]);
    }

    [Fact]
    public void What_moved_is_both_the_subtotal_and_the_total_with_nothing_to_round()
    {
        PrintPayload payload = BuildMoney();

        Assert.Equal(4_000m, payload.Singles["Totals.SubTotal"]);
        Assert.Equal(4_000m, payload.Singles["Totals.GrandTotal"]);
        Assert.Equal(0m, payload.Singles["Totals.RoundOff"]);
    }

    [Fact]
    public void A_draft_has_no_number_yet_and_prints_as_none()
    {
        PrintPayload payload = MoneyPrintPayloadRepository.Build(
            Header(MoneyDocumentStatus.Draft, number: null), [], Org, "V", null);

        Assert.Null(payload.Singles["Document.No"]);
        Assert.Null(payload.Singles["Document.BankAccountName"]);
    }

    [Fact]
    public void Lines_name_the_document_they_settled_in_line_order()
    {
        PrintPayload payload = BuildMoney(
            new MoneyPrintLine(2, 17, null, null, null, 1_000m, null),
            new MoneyPrintLine(1, 3, "INV-0042", new DateOnly(2026, 10, 1), 3_000m, 3_000m, null));

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows = payload.Rows("Alloc");

        Assert.Equal(2, rows.Count);
        Assert.Equal("INV-0042", rows[0]["Alloc.DocumentNo"]);
        Assert.Equal(new DateOnly(2026, 10, 1), rows[0]["Alloc.DocumentDate"]);
        Assert.Equal(3_000m, rows[0]["Alloc.DocumentTotal"]);
        Assert.Equal(3_000m, rows[0]["Alloc.Amount"]);
    }

    [Fact]
    public void A_line_that_settles_no_document_says_what_it_is_instead()
    {
        PrintPayload payload = BuildMoney(new MoneyPrintLine(1, 17, null, null, null, 1_000m, null));

        IReadOnlyDictionary<string, object?> row = Assert.Single(payload.Rows("Alloc"));

        Assert.Equal("Overpayment from customer", row["Alloc.DocumentNo"]);
        Assert.Null(row["Alloc.DocumentDate"]);
        Assert.Null(row["Alloc.DocumentTotal"]);
    }

    [Fact]
    public void Every_ledger_source_a_line_can_carry_has_its_own_label()
    {
        int[] sources = [2, 3, 4, 6, 7, 8, 9, 16, 17, 18, 19, 20];

        string[] labels = [.. sources.Select(MoneyPrintPayloadRepository.SourceLabel)];

        Assert.Equal(labels.Length, labels.Distinct().Count());
        Assert.DoesNotContain("Payment", labels);
    }

    [Theory]
    [InlineData(PaymentMethod.Cash, "Cash")]
    [InlineData(PaymentMethod.Cheque, "Cheque")]
    [InlineData(PaymentMethod.BankTransfer, "Bank transfer")]
    [InlineData(PaymentMethod.Upi, "UPI")]
    [InlineData(PaymentMethod.Card, "Card")]
    [InlineData(PaymentMethod.DemandDraft, "Demand draft")]
    [InlineData(PaymentMethod.Wallet, "Wallet")]
    [InlineData(PaymentMethod.Other, "Other")]
    public void A_payment_method_reads_as_a_person_would_say_it(PaymentMethod method, string expected)
    {
        Assert.Equal(expected, MoneyPrintPayloadRepository.PaymentLabel(method));
    }

    [Theory]
    [InlineData(MoneyDocumentStatus.Posted, null)]
    [InlineData(MoneyDocumentStatus.Draft, "DRAFT")]
    [InlineData(MoneyDocumentStatus.Void, "VOID")]
    public void Only_a_posted_money_document_prints_without_a_stamp(MoneyDocumentStatus status, string? expected)
    {
        Assert.Equal(expected, MoneyPrintPayloadRepository.Watermark(status));
    }

    [Fact]
    public void Every_tag_a_receipt_writes_is_one_the_catalogue_offers_for_its_type()
    {
        PrintPayload payload = BuildMoney(new MoneyPrintLine(1, 3, "INV-0042", new DateOnly(2026, 10, 1), 3_000m, 3_000m, null));

        IEnumerable<string> keys = payload.Singles.Keys
            .Concat(payload.Lists.Values.SelectMany(rows => rows.SelectMany(row => row.Keys)))
            .Distinct();

        Assert.All(keys.Where(key => key != "Document.ExchangeRate"), key => Assert.NotNull(PlaceholderCatalog.Find("RCM", key)));
        Assert.All(keys.Where(key => key != "Document.ExchangeRate"), key => Assert.NotNull(PlaceholderCatalog.Find("SPM", key)));
    }

    private static JournalPrintHeader JournalHeader(JournalStatus status = JournalStatus.Posted) => new(
        "JV/2627/00003", new DateOnly(2026, 11, 2), status, "INR", 1m, "Audit adjustment", "Year-end true-up");

    [Fact]
    public void A_journal_prints_its_lines_in_order_with_the_sub_account_beneath_the_account()
    {
        PrintPayload payload = JournalPrintPayloadRepository.Build(
            JournalHeader(),
            [
                new JournalPrintLine(2, "4100", "Sales Revenue", null, null, 0m, 500m),
                new JournalPrintLine(1, "1100", "Accounts Receivable", "Karnataka Traders", "True-up", 500m, 0m),
            ],
            Org);

        IReadOnlyList<IReadOnlyDictionary<string, object?>> lines = payload.Rows("Line");

        Assert.Equal("1100", lines[0]["Line.AccountCode"]);
        Assert.Equal("Accounts Receivable — Karnataka Traders", lines[0]["Line.AccountName"]);
        Assert.Equal("True-up", lines[0]["Line.Description"]);
        Assert.Equal("Sales Revenue", lines[1]["Line.AccountName"]);
    }

    [Fact]
    public void A_line_prints_only_the_column_it_uses()
    {
        PrintPayload payload = JournalPrintPayloadRepository.Build(
            JournalHeader(),
            [new JournalPrintLine(1, "1100", "Accounts Receivable", null, null, 500m, 0m)],
            Org);

        IReadOnlyDictionary<string, object?> line = Assert.Single(payload.Rows("Line"));

        Assert.Equal(500m, line["Line.Debit"]);
        Assert.Null(line["Line.Credit"]);
    }

    [Fact]
    public void A_journal_has_no_party_and_its_total_is_what_it_moves()
    {
        PrintPayload payload = JournalPrintPayloadRepository.Build(
            JournalHeader(),
            [
                new JournalPrintLine(1, "1100", "Accounts Receivable", null, null, 500m, 0m),
                new JournalPrintLine(2, "4100", "Sales Revenue", null, null, 0m, 500m),
            ],
            Org);

        Assert.False(payload.Singles.ContainsKey("Party.Name"));
        Assert.Equal("JV/2627/00003", payload.Singles["Document.No"]);
        Assert.Equal("Audit adjustment", payload.Singles["Document.Reference"]);
        Assert.Equal("Year-end true-up", payload.Singles["Document.Notes"]);
        Assert.Equal(500m, payload.Singles["Totals.GrandTotal"]);
    }

    [Theory]
    [InlineData(JournalStatus.Posted, null)]
    [InlineData(JournalStatus.Draft, "DRAFT")]
    [InlineData(JournalStatus.Reversed, "REVERSED")]
    public void A_reversed_journal_says_so_and_a_draft_is_not_final(JournalStatus status, string? expected)
    {
        Assert.Equal(expected, JournalPrintPayloadRepository.Watermark(status));
    }

    [Fact]
    public void Every_tag_a_journal_writes_is_one_the_catalogue_offers_for_its_type()
    {
        PrintPayload payload = JournalPrintPayloadRepository.Build(
            JournalHeader(),
            [new JournalPrintLine(1, "1100", "Accounts Receivable", null, "x", 500m, 0m)],
            Org);

        IEnumerable<string> keys = payload.Singles.Keys
            .Concat(payload.Lists.Values.SelectMany(rows => rows.SelectMany(row => row.Keys)))
            .Distinct();

        Assert.All(keys.Where(key => key != "Document.ExchangeRate"), key => Assert.NotNull(PlaceholderCatalog.Find("JRN", key)));
    }
}

/// <summary>
/// A receipt, a payment and a journal laid out through their repository over a real database: what each sends
/// Printing, which template type it asks for, and what it tells the view page about itself (TK-263). Written
/// by an AI and not run (section 0.5 of docs/Task_Details.md).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AccountingPrintTests
{
    private static readonly DateOnly Dated = new(2026, 11, 2);

    private readonly PostgresFixture _postgres;

    public AccountingPrintTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed class RecordingPrinting : IPrintingClient
    {
        public List<(string Type, PrintPayload Payload, string? Watermark)> Printed { get; } = [];

        public Task<PrintedDocument> RenderAsync(
            string documentTypeCode, long? printTemplateId, PrintPayload payload, string? watermark, CancellationToken ct)
        {
            Printed.Add((documentTypeCode, payload, watermark));
            return Task.FromResult(new PrintedDocument { Html = "<p>laid out</p>", PageCount = 1 });
        }
    }

    private sealed class Branch(OrgIdentity? identity) : IOrgIdentityProvider
    {
        public Task<OrgIdentity?> GetIdentityAsync(CancellationToken ct = default) => Task.FromResult(identity);
    }

    private sealed class Names : IContactNameLookup
    {
        public Task<IReadOnlyDictionary<long, NamedRef>> ResolveAsync(
            IReadOnlyCollection<long> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<long, NamedRef>>(
                ids.ToDictionary(id => id, id => new NamedRef(id, $"C{id}", $"Name {id}")));
    }

    private sealed record Arrangement(
        AccountingDbContext Db,
        AccountingPrintRepository Printing,
        RecordingPrinting Recorder,
        long ReceivableId,
        long RevenueId,
        long BankAccountId);

    private async Task<Arrangement> ArrangeAsync(Guid? customerId = null, Guid? orgId = null, bool resolvesBranch = true)
    {
        Guid org = orgId ?? Guid.NewGuid();
        AccountingDbContext db = _postgres.CreateContext(customerId ?? Guid.NewGuid(), org);

        async Task<long> Account(string code, string name, int typeId)
        {
            var account = new Account { OrgId = org, AccountTypeId = typeId, AccountCode = code, AccountName = name, IsActive = true };
            db.Accounts.Add(account);
            await db.SaveChangesAsync();
            return account.AccountId;
        }

        long receivable = await Account("1100", "Accounts Receivable", 1);
        long revenue = await Account("4100", "Sales Revenue", 4);
        long bankLedger = await Account("1020", "Bank", 1);

        var bank = new BankAccount
        {
            OrgId = org,
            AccountName = "HDFC Current",
            AccountNumber = "000111",
            AccountType = BankAccountType.Current,
            CurrencyCode = "INR",
            LedgerAccountId = bankLedger,
            IsActive = true,
        };
        db.BankAccounts.Add(bank);
        await db.SaveChangesAsync();

        var recorder = new RecordingPrinting();
        OrgIdentity? identity = resolvesBranch
            ? new OrgIdentity("Test Traders", "33AAAAA0000A1Z5", "1 Test Street", null, "Chennai", "33", "600001")
            : null;

        return new Arrangement(
            db, new AccountingPrintRepository(db, new Branch(identity), new Names(), recorder),
            recorder, receivable, revenue, bank.BankAccountId);
    }

    [SkippableFact]
    public async Task A_receipt_names_the_invoice_it_settled_with_its_number_date_and_total()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Arrangement a = await ArrangeAsync();
        await using (a.Db)
        {
            var contactAccount = new SubAccount
            {
                OrgId = a.Db.CurrentOrgId,
                AccountTypeId = 1,
                AccountId = a.ReceivableId,
                ReferenceType = SubAccountReferenceType.Contact,
                ReferenceId = 7,
                SubAccountName = "Name 7",
            };
            a.Db.SubAccounts.Add(contactAccount);
            await a.Db.SaveChangesAsync();

            // The invoice's own legs, as Sales posts them: the receivable debited under the contact's
            // sub-account, the revenue credited.
            a.Db.JournalLedger.AddRange(
                new JournalLedger
                {
                    OrgId = a.Db.CurrentOrgId, AccountId = a.ReceivableId, SubAccountId = contactAccount.SubAccountId,
                    TransactionTypeCode = "INV", TransactionId = 900, LedgerDate = new DateOnly(2026, 10, 1),
                    CurrencyCode = "INR", ExchangeRate = 1m, ContactId = 7, DocumentNo = "INV-0042",
                    LedgerTypeId = MoneyPostingMapRepository.ControlLedgerType,
                    DebitAmount = 5_000m, DebitAmountBase = 5_000m,
                },
                new JournalLedger
                {
                    OrgId = a.Db.CurrentOrgId, AccountId = a.RevenueId,
                    TransactionTypeCode = "INV", TransactionId = 900, LedgerDate = new DateOnly(2026, 10, 1),
                    CurrencyCode = "INR", ExchangeRate = 1m, ContactId = 7, DocumentNo = "INV-0042",
                    LedgerTypeId = 1,
                    CreditAmount = 5_000m, CreditAmountBase = 5_000m,
                });

            var receipt = new ReceiveMoney
            {
                OrgId = a.Db.CurrentOrgId,
                TransactionNo = "REC/2627/00001",
                TransactionDate = Dated,
                BankAccountId = a.BankAccountId,
                ContactId = 7,
                Amount = 5_500m,
                CurrencyCode = "INR",
                ExchangeRate = 1m,
                PaymentMethod = PaymentMethod.Upi,
                ReferenceNo = "UTR 1",
                Status = MoneyDocumentStatus.Posted,
            };
            a.Db.ReceiveMoney.Add(receipt);
            await a.Db.SaveChangesAsync();

            a.Db.ReceiveMoneyDetails.AddRange(
                new ReceiveMoneyDetail
                {
                    OrgId = a.Db.CurrentOrgId, ReceiveMoneyId = receipt.ReceiveMoneyId, LineNumber = 1, LedgerSourceId = 3,
                    MappingTransactionTypeCode = "INV", MappingTransactionId = 900, Amount = 5_000m, AmountBase = 5_000m,
                },
                new ReceiveMoneyDetail
                {
                    OrgId = a.Db.CurrentOrgId, ReceiveMoneyId = receipt.ReceiveMoneyId, LineNumber = 2, LedgerSourceId = 17,
                    Amount = 500m, AmountBase = 500m,
                });
            await a.Db.SaveChangesAsync();

            PrintedDocument? printed = await a.Printing.PrintReceiptAsync(receipt.ReceiveMoneyId, default);

            Assert.NotNull(printed);
            Assert.Equal("REC/2627/00001", printed.DocumentNo);
            Assert.Equal("RCM", printed.DocumentTypeCode);
            Assert.Equal("Posted", printed.Status);

            var sent = Assert.Single(a.Recorder.Printed);
            Assert.Equal("RCM", sent.Type);
            Assert.Null(sent.Watermark);
            Assert.Equal("Name 7", sent.Payload.Singles["Party.Name"]);
            Assert.Equal("HDFC Current", sent.Payload.Singles["Document.BankAccountName"]);

            IReadOnlyList<IReadOnlyDictionary<string, object?>> rows = sent.Payload.Rows("Alloc");
            Assert.Equal("INV-0042", rows[0]["Alloc.DocumentNo"]);
            Assert.Equal(new DateOnly(2026, 10, 1), rows[0]["Alloc.DocumentDate"]);
            Assert.Equal(5_000m, rows[0]["Alloc.DocumentTotal"]);
            Assert.Equal("Overpayment from customer", rows[1]["Alloc.DocumentNo"]);
        }
    }

    [SkippableFact]
    public async Task A_draft_payment_prints_stamped_draft_with_no_number()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Arrangement a = await ArrangeAsync();
        await using (a.Db)
        {
            var payment = new SpendMoney
            {
                OrgId = a.Db.CurrentOrgId,
                TransactionDate = Dated,
                BankAccountId = a.BankAccountId,
                ContactId = 8,
                Amount = 800m,
                CurrencyCode = "INR",
                ExchangeRate = 1m,
            };
            a.Db.SpendMoney.Add(payment);
            await a.Db.SaveChangesAsync();

            PrintedDocument? printed = await a.Printing.PrintPaymentAsync(payment.SpendMoneyId, default);

            Assert.NotNull(printed);
            Assert.Null(printed.DocumentNo);
            Assert.Equal("SPM", printed.DocumentTypeCode);
            Assert.Equal("Draft", printed.Status);
            Assert.Equal("DRAFT", Assert.Single(a.Recorder.Printed).Watermark);
        }
    }

    [SkippableFact]
    public async Task A_journal_prints_each_lines_account_and_sub_account()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Arrangement a = await ArrangeAsync();
        await using (a.Db)
        {
            var contactAccount = new SubAccount
            {
                OrgId = a.Db.CurrentOrgId,
                AccountTypeId = 1,
                AccountId = a.ReceivableId,
                ReferenceType = SubAccountReferenceType.Contact,
                ReferenceId = 7,
                SubAccountName = "Name 7",
            };
            a.Db.SubAccounts.Add(contactAccount);
            await a.Db.SaveChangesAsync();

            var journal = new Journal
            {
                OrgId = a.Db.CurrentOrgId,
                JournalNo = "JV/2627/00003",
                JournalDate = Dated,
                CurrencyCode = "INR",
                ExchangeRate = 1m,
                Reference = "Audit",
                Status = JournalStatus.Posted,
                PostedAt = DateTimeOffset.UtcNow,
            };
            a.Db.Journals.Add(journal);
            await a.Db.SaveChangesAsync();

            a.Db.JournalDetails.AddRange(
                new JournalDetail
                {
                    OrgId = a.Db.CurrentOrgId, JournalId = journal.JournalId, LineNumber = 1, AccountId = a.ReceivableId,
                    SubAccountId = contactAccount.SubAccountId, DebitAmount = 500m, DebitAmountBase = 500m, LineMemo = "True-up",
                },
                new JournalDetail
                {
                    OrgId = a.Db.CurrentOrgId, JournalId = journal.JournalId, LineNumber = 2, AccountId = a.RevenueId,
                    CreditAmount = 500m, CreditAmountBase = 500m,
                });
            await a.Db.SaveChangesAsync();

            PrintedDocument? printed = await a.Printing.PrintJournalAsync(journal.JournalId, default);

            Assert.NotNull(printed);
            Assert.Equal("JV/2627/00003", printed.DocumentNo);
            Assert.Equal("JRN", printed.DocumentTypeCode);

            var sent = Assert.Single(a.Recorder.Printed);
            Assert.Null(sent.Watermark);
            IReadOnlyList<IReadOnlyDictionary<string, object?>> lines = sent.Payload.Rows("Line");
            Assert.Equal("Accounts Receivable — Name 7", lines[0]["Line.AccountName"]);
            Assert.Equal("1100", lines[0]["Line.AccountCode"]);
            Assert.Equal(500m, lines[0]["Line.Debit"]);
            Assert.Equal("Sales Revenue", lines[1]["Line.AccountName"]);
            Assert.Equal(500m, lines[1]["Line.Credit"]);
        }
    }

    [SkippableFact]
    public async Task A_document_in_no_branch_of_the_callers_is_not_found_and_nothing_is_rendered()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Arrangement a = await ArrangeAsync();
        await using (a.Db)
        {
            Assert.Null(await a.Printing.PrintReceiptAsync(987_654, default));
            Assert.Null(await a.Printing.PrintPaymentAsync(987_654, default));
            Assert.Null(await a.Printing.PrintJournalAsync(987_654, default));
            Assert.Empty(a.Recorder.Printed);
        }
    }

    [SkippableFact]
    public async Task A_branch_that_cannot_be_identified_stops_the_print_rather_than_printing_a_placeholder()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Arrangement a = await ArrangeAsync(resolvesBranch: false);
        await using (a.Db)
        {
            var journal = new Journal
            {
                OrgId = a.Db.CurrentOrgId, JournalDate = Dated, CurrencyCode = "INR", ExchangeRate = 1m, Status = JournalStatus.Draft,
            };
            a.Db.Journals.Add(journal);
            await a.Db.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => a.Printing.PrintJournalAsync(journal.JournalId, default));
            Assert.Empty(a.Recorder.Printed);
        }
    }
}
