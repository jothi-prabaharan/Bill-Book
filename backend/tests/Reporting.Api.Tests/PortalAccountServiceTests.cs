using Reporting.Api.Services;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The client portal's dashboard figures and statement (TK-95), over one
/// contact's books worked out by hand beside each figure. Over lists, like the
/// report sources: this proves the arithmetic, not the SQL translation.
/// </summary>
public sealed class PortalAccountServiceTests
{
    private const long Contact = 42;
    private const long Stranger = 43;

    private const long Trade = 1;
    private const long Prepayment = 2;
    private const long Payable = 3;
    private const long StrangerTrade = 9;

    private static readonly DateTimeOffset Today = new(2026, 9, 26, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Outstanding_is_the_receivable_net_of_advances_and_ignores_other_contacts()
    {
        PortalSummary summary = await Service().SummaryAsync(Contact, default);

        // Trade: 1,180 − 500 + 2,360 + 590 − 590 = 3,040. Less the 300 advance.
        Assert.Equal(2_740m, summary.Outstanding);
    }

    [Fact]
    public async Task Overdue_is_what_is_still_owed_on_invoices_past_their_due_date()
    {
        PortalSummary summary = await Service().SummaryAsync(Contact, default);

        // INV 100 was due 31 May with 680 left; INV 90 is overdue but paid;
        // INV 101 is not due until October.
        Assert.Equal(680m, summary.Overdue);
    }

    [Fact]
    public async Task Overdue_never_exceeds_what_is_owed_net_of_advances()
    {
        Books books = new();
        books.SubAccount(21, 44, 1);
        books.Invoice(300, 44, 300m, new DateOnly(2026, 8, 1), due: new DateOnly(2026, 8, 31));
        books.Leg(21, "INV", 300, debit: 300m, on: new DateOnly(2026, 8, 1));
        books.Leg(21, "RCM", 77, credit: 250m, on: new DateOnly(2026, 9, 1), ledgerType: 3);

        PortalSummary summary = await Service(books).SummaryAsync(44, default);

        Assert.Equal(50m, summary.Outstanding);
        Assert.Equal(50m, summary.Overdue);
    }

    [Fact]
    public async Task Trade_value_is_posted_invoices_less_posted_credit_notes_this_year_and_in_all()
    {
        PortalSummary summary = await Service().SummaryAsync(Contact, default);

        // The year began 1 April 2026: 1,180 + 2,360 − 118. All time adds last
        // December's 590. The draft and the voided invoice count for nothing.
        Assert.Equal(new DateOnly(2026, 4, 1), summary.FinancialYearStart);
        Assert.Equal(3_422m, summary.TradeValueThisYear);
        Assert.Equal(4_012m, summary.TradeValueAllTime);
    }

    [Fact]
    public async Task The_recent_documents_are_the_newest_posted_ones_first()
    {
        PortalSummary summary = await Service().SummaryAsync(Contact, default);

        Assert.Equal(["INV/26/0101", "CN/26/0001", "INV/26/0100", "INV/25/0090"], summary.Recent.Select(d => d.DocumentNo));
    }

    [Theory]
    [InlineData(2026, 9, 26, 4, 2026, 4)]
    [InlineData(2026, 3, 31, 4, 2025, 4)]
    [InlineData(2026, 1, 1, 1, 2026, 1)]
    [InlineData(2026, 5, 1, 13, 2026, 4)]
    public void The_financial_year_starts_in_the_branchs_month(int y, int m, int d, int startMonth, int startYear, int expectedMonth) =>
        Assert.Equal(new DateOnly(startYear, expectedMonth, 1), PortalAccountService.FinancialYearStart(new DateOnly(y, m, d), startMonth));

    [Fact]
    public async Task The_statement_runs_the_receivable_from_an_opening_balance_and_names_documents_by_number()
    {
        PortalStatement statement = await Service().StatementAsync(Contact, new DateOnly(2026, 6, 1), null, default);

        // Before June: 1,180 − 500 + 590 − 590 = 680. Then the advance and INV 101.
        Assert.Equal(680m, statement.Receivable.OpeningBalance);
        Assert.Equal([380m, 2_740m], statement.Receivable.Transactions.Select(t => t.Balance));
        Assert.Equal(2_740m, statement.Receivable.ClosingBalance);
        Assert.Equal("INV/26/0101", statement.Receivable.Transactions[^1].DocumentNo);

        // A row posted before the ledger carried the number falls back to code and id.
        Assert.Equal("RCM-7", statement.Receivable.Transactions[0].DocumentNo);
    }

    [Fact]
    public async Task A_contact_who_is_also_a_vendor_sees_what_they_are_owed_as_a_positive_balance()
    {
        PortalStatement statement = await Service().StatementAsync(Contact, null, null, default);

        PortalStatementSide payable = Assert.IsType<PortalStatementSide>(statement.Payable);
        Assert.Equal([1_000m, 600m], payable.Transactions.Select(t => t.Balance));
        Assert.Equal(600m, payable.ClosingBalance);
    }

    [Fact]
    public async Task A_customer_only_contact_has_no_payable_side_and_sees_no_other_contacts_rows()
    {
        PortalStatement statement = await Service().StatementAsync(Stranger, null, null, default);

        Assert.Null(statement.Payable);
        LedgerLine only = Assert.Single(statement.Receivable.Transactions.Select(t => new LedgerLine(t.TransactionId, t.Debit)));
        Assert.Equal(new LedgerLine(200, 999m), only);
    }

    private sealed record LedgerLine(long TransactionId, decimal Debit);

    private static PortalAccountService Service(Books? books = null) =>
        new(books ?? Standard(), new AprilYear(), new FixedClock(Today));

    /// <summary>The worked books every figure above is checked against.</summary>
    private static Books Standard()
    {
        Books b = new();
        b.SubAccount(Trade, Contact, 1);
        b.SubAccount(Prepayment, Contact, 1);
        b.SubAccount(Payable, Contact, 2);
        b.SubAccount(StrangerTrade, Stranger, 1);

        b.Invoice(90, Contact, 590m, new DateOnly(2025, 12, 1), due: new DateOnly(2025, 12, 31), no: "INV/25/0090");
        b.Invoice(100, Contact, 1_180m, new DateOnly(2026, 5, 1), due: new DateOnly(2026, 5, 31), no: "INV/26/0100");
        b.Invoice(101, Contact, 2_360m, new DateOnly(2026, 9, 20), due: new DateOnly(2026, 10, 20), no: "INV/26/0101");
        b.Invoice(102, Contact, 5_000m, new DateOnly(2026, 9, 21), status: DocumentStatus.Draft);
        b.Invoice(103, Contact, 700m, new DateOnly(2026, 9, 22), status: DocumentStatus.Void);
        b.Invoice(200, Stranger, 999m, new DateOnly(2026, 9, 1));
        b.CreditNote(1, Contact, 118m, new DateOnly(2026, 6, 1), "CN/26/0001");
        b.CreditNote(2, Contact, 50m, new DateOnly(2026, 6, 2), "CN/26/0002", DocumentStatus.Draft);

        b.Leg(Trade, "INV", 90, debit: 590m, on: new DateOnly(2025, 12, 1), no: "INV/25/0090");
        b.Leg(Trade, "INV", 90, credit: 590m, on: new DateOnly(2026, 1, 10), no: "INV/25/0090");
        b.Leg(Trade, "INV", 100, debit: 1_180m, on: new DateOnly(2026, 5, 1), no: "INV/26/0100");
        b.Leg(Trade, "INV", 100, credit: 500m, on: new DateOnly(2026, 5, 15), no: "INV/26/0100");
        b.Leg(Prepayment, "RCM", 7, credit: 300m, on: new DateOnly(2026, 7, 1));
        b.Leg(Trade, "INV", 101, debit: 2_360m, on: new DateOnly(2026, 9, 20), no: "INV/26/0101");
        b.Leg(Payable, "BIL", 55, credit: 1_000m, on: new DateOnly(2026, 6, 10), no: "BIL/26/0055");
        b.Leg(Payable, "BIL", 55, debit: 400m, on: new DateOnly(2026, 7, 10), no: "BIL/26/0055");
        b.Leg(StrangerTrade, "INV", 200, debit: 999m, on: new DateOnly(2026, 9, 1), no: "INV/26/0200");
        return b;
    }

    private sealed class Books : IPortalAccountData
    {
        private readonly List<JournalLedgerRead> _ledger = [];
        private readonly List<SubAccountRead> _subAccounts = [];
        private readonly List<InvoiceRead> _invoices = [];
        private readonly List<CreditNoteRead> _credits = [];

        public IQueryable<JournalLedgerRead> Ledger => AsyncQueryable.Of(_ledger);

        public IQueryable<SubAccountRead> SubAccounts => AsyncQueryable.Of(_subAccounts);

        public IQueryable<InvoiceRead> Invoices => AsyncQueryable.Of(_invoices);

        public IQueryable<CreditNoteRead> CreditNotes => AsyncQueryable.Of(_credits);

        public void SubAccount(long id, long contactId, int accountTypeId) => _subAccounts.Add(new SubAccountRead
        {
            SubAccountId = id,
            AccountId = accountTypeId == 1 ? 1100 : 2100,
            AccountTypeId = accountTypeId,
            ReferenceType = 1,
            ReferenceId = contactId,
            SubAccountName = $"Contact {contactId}",
        });

        public void Invoice(long id, long contactId, decimal total, DateOnly on, DateOnly? due = null,
            DocumentStatus status = DocumentStatus.Posted, string? no = null) => _invoices.Add(new InvoiceRead
        {
            InvoiceId = id,
            TransactionTypeCode = "INV",
            DocumentNo = no ?? $"INV/{id}",
            DocumentDate = on,
            DueDate = due,
            ContactId = contactId,
            TotalAmount = total,
            TotalAmountBase = total,
            Status = status,
        });

        public void CreditNote(long id, long contactId, decimal total, DateOnly on, string no,
            DocumentStatus status = DocumentStatus.Posted) => _credits.Add(new CreditNoteRead
        {
            CreditNoteId = id,
            TransactionTypeCode = "CRN",
            DocumentNo = no,
            DocumentDate = on,
            ContactId = contactId,
            TotalAmount = total,
            TotalAmountBase = total,
            Status = status,
        });

        public void Leg(long subAccountId, string code, long transactionId, DateOnly on,
            decimal debit = 0m, decimal credit = 0m, string? no = null, int ledgerType = 3) => _ledger.Add(new JournalLedgerRead
        {
            LedgerId = _ledger.Count + 1,
            LedgerDate = on,
            AccountId = 1100,
            SubAccountId = subAccountId,
            TransactionTypeCode = code,
            TransactionId = transactionId,
            DebitAmount = debit,
            CreditAmount = credit,
            DebitAmountBase = debit,
            CreditAmountBase = credit,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            LedgerTypeId = ledgerType,
            LedgerSourceId = 1,
            DocumentNo = no,
        });
    }

    private sealed class AprilYear : IFinancialYearProvider
    {
        public Task<int> GetStartMonthAsync(CancellationToken ct = default) => Task.FromResult(4);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
