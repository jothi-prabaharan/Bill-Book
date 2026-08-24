using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sales.Api.Services;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// Sales Invoice posting, general ledger double-entry, inventory movement,
/// and lifecycle immutability verification against a real PostgreSQL database.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class InvoicePostingTests
{
    private const string InventoryAccount = "Inventory";
    private const string GdniAccount = "Goods Delivered Not Invoiced";
    private const string CogsAccount = "Cost of Goods Sold";
    private const string SalesRevenueAccount = "Sales";
    private const string TaxPayableAccount = "Tax Payable";
    private const string AccountsReceivableAccount = "Accounts Receivable";
    private const string CashAccount = "Cash";
    private const string RoundOffAccount = "Round Off";

    private const long ItemId = 101;
    private const long ContactId = 42;

    private readonly PostgresFixture _pg;

    public InvoicePostingTests(PostgresFixture pg) => _pg = pg;

    [SkippableFact]
    public async Task Posting_standard_invoice_creates_balanced_double_entry_ledger_legs()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        // 10 qty at 100 with 18% GST (9% CGST, 9% SGST) -> Taxable: 1000, Tax: 180, Total: 1180
        InvoiceResult created = await h.Invoices.CreateAsync(
            Request(lines: [Line(quantity: 10m, unitPrice: 100m)]),
            CancellationToken.None);

        Assert.Equal(InvoiceOutcome.Ok, created.Outcome);

        InvoiceResult posted = await h.Invoices.PostAsync(created.InvoiceId, CancellationToken.None);
        Assert.Equal(InvoiceOutcome.Ok, posted.Outcome);

        PostLedgerRequest post = Assert.Single(h.Ledger.Posts);

        // Total Debit == Total Credit
        decimal totalDebit = post.Legs.Sum(l => l.DebitAmount);
        decimal totalCredit = post.Legs.Sum(l => l.CreditAmount);
        Assert.Equal(totalDebit, totalCredit);

        // Accounts Receivable debited for 1180
        LedgerLegRequest arLeg = Assert.Single(post.Legs, l => l.AccountSystemName == AccountsReceivableAccount);
        Assert.Equal(1180m, arLeg.DebitAmount);
        Assert.Equal(0m, arLeg.CreditAmount);
        Assert.Equal(1, arLeg.SubAccountReferenceType);
        Assert.Equal(ContactId, arLeg.SubAccountReferenceId);

        // Sales Revenue credited for 1000
        LedgerLegRequest salesLeg = Assert.Single(post.Legs, l => l.AccountSystemName == SalesRevenueAccount);
        Assert.Equal(1000m, salesLeg.CreditAmount);
        Assert.Equal(0m, salesLeg.DebitAmount);

        // Tax Payable credited for 180 (90 CGST + 90 SGST)
        var taxLegs = post.Legs.Where(l => l.AccountSystemName == TaxPayableAccount).ToList();
        Assert.Equal(180m, taxLegs.Sum(l => l.CreditAmount));

        // COGS & Inventory relief legs
        Assert.Equal(500m, h.Ledger.DebitOf(CogsAccount));
        Assert.Equal(500m, h.Ledger.CreditOf(InventoryAccount));
    }

    [SkippableFact]
    public async Task POS_sale_debits_cash_instead_of_accounts_receivable()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        var req = Request(lines: [Line(quantity: 2m, unitPrice: 50m)]);
        req.TillId = 5;
        req.PaymentMode = "Cash";
        req.TenderedAmount = 118m;
        req.ChangeAmount = 0m;
        req.DueDate = null;

        InvoiceResult created = await h.Invoices.CreateAsync(req, CancellationToken.None);
        Assert.Equal(InvoiceOutcome.Ok, created.Outcome);

        InvoiceResult posted = await h.Invoices.PostAsync(created.InvoiceId, CancellationToken.None);
        Assert.Equal(InvoiceOutcome.Ok, posted.Outcome);

        PostLedgerRequest post = Assert.Single(h.Ledger.Posts);

        // Cash debited 118 (100 goods + 18 tax)
        LedgerLegRequest cashLeg = Assert.Single(post.Legs, l => l.AccountSystemName == CashAccount);
        Assert.Equal(118m, cashLeg.DebitAmount);
        Assert.Null(cashLeg.SubAccountReferenceType);
        Assert.Null(cashLeg.SubAccountReferenceId);

        // No AR leg
        Assert.DoesNotContain(post.Legs, l => l.AccountSystemName == AccountsReceivableAccount);
    }

    [SkippableFact]
    public async Task Posting_invoice_with_sales_order_releases_reservation()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        (Guid customerId, Guid orgId) = h.Tenant.Require();
        var salesOrder = new SalesOrder
        {
            OrgId = orgId,
            TransactionTypeCode = "SOR",
            DocumentNo = "SO/26/00001",
            DocumentDate = new DateOnly(2026, 6, 1),
            ContactId = ContactId,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Status = DocumentStatus.Posted,
            PostedAt = DateTimeOffset.UtcNow,
            PostedBy = Guid.NewGuid(),
            Lines = [],
        };
        h.Db.SalesOrders.Add(salesOrder);
        await h.Db.SaveChangesAsync();

        var req = Request(lines: [Line(quantity: 5m, unitPrice: 100m)]);
        req.SalesOrderId = salesOrder.SalesOrderId;

        InvoiceResult created = await h.Invoices.CreateAsync(req, CancellationToken.None);
        Assert.Equal(InvoiceOutcome.Ok, created.Outcome);

        await h.Invoices.PostAsync(created.InvoiceId, CancellationToken.None);

        IssueStockRequest issueCall = Assert.Single(h.Inventory.Issues);
        Assert.Equal("INV", issueCall.SourceType);
        Assert.Equal(created.InvoiceId, issueCall.SourceId);
        Assert.True(issueCall.Lines.Single().ReleaseReservation);
    }

    [SkippableFact]
    public async Task Posting_direct_invoice_does_not_release_reservation()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        var req = Request(lines: [Line(quantity: 5m, unitPrice: 100m)]);
        req.SalesOrderId = null;

        InvoiceResult created = await h.Invoices.CreateAsync(req, CancellationToken.None);
        Assert.Equal(InvoiceOutcome.Ok, created.Outcome);

        await h.Invoices.PostAsync(created.InvoiceId, CancellationToken.None);

        IssueStockRequest issueCall = Assert.Single(h.Inventory.Issues);
        Assert.False(issueCall.Lines.Single().ReleaseReservation);
    }

    [SkippableFact]
    public async Task Posting_invoice_from_delivery_challan_does_not_issue_stock_and_credits_gdni()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        // Create and save delivery challan
        (Guid customerId, Guid orgId) = h.Tenant.Require();
        var challan = new DeliveryChallan
        {
            OrgId = orgId,
            TransactionTypeCode = "DLC",
            DocumentNo = "DC/26/00001",
            DocumentDate = new DateOnly(2026, 6, 1),
            ContactId = ContactId,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Status = DocumentStatus.Posted,
            PostedAt = DateTimeOffset.UtcNow,
            PostedBy = Guid.NewGuid(),
            Lines = [],
        };
        h.Db.DeliveryChallans.Add(challan);
        await h.Db.SaveChangesAsync();

        var challanDetail = new DeliveryChallanDetail
        {
            DeliveryChallanId = challan.DeliveryChallanId,
            OrgId = orgId,
            LineNumber = 1,
            ItemId = ItemId,
            Quantity = 10m,
            BaseQuantity = 10m,
            ConversionFactor = 1m,
            UnitCost = 45m,
            StockMovementId = 9999,
            UnitPrice = 100m,
            GrossAmount = 1000m,
            TaxableAmount = 1000m,
            TaxAmount = 0m,
            LineTotal = 1000m,
        };
        h.Db.DeliveryChallanDetails.Add(challanDetail);
        await h.Db.SaveChangesAsync();
        challan.Lines.Add(challanDetail);

        var req = Request(lines: [Line(quantity: 10m, unitPrice: 100m)]);
        req.DeliveryChallanId = challan.DeliveryChallanId;

        InvoiceResult created = await h.Invoices.CreateAsync(req, CancellationToken.None);
        Assert.Equal(InvoiceOutcome.Ok, created.Outcome);

        InvoiceResult posted = await h.Invoices.PostAsync(created.InvoiceId, CancellationToken.None);
        Assert.Equal(InvoiceOutcome.Ok, posted.Outcome);

        // Inventory client IssueAsync is NOT called because goods already left via challan
        Assert.Empty(h.Inventory.Issues);

        // COGS debited (45 * 10 = 450) and GDNI credited
        Assert.Equal(450m, h.Ledger.DebitOf(CogsAccount));
        Assert.Equal(450m, h.Ledger.CreditOf(GdniAccount));
        Assert.Equal(0m, h.Ledger.CreditOf(InventoryAccount));
    }

    [SkippableFact]
    public async Task Posting_invoice_synchronously_populates_sales_register()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        var req = Request(lines: [Line(quantity: 2m, unitPrice: 500m)]);
        req.ContactGstin = "33AAAAA0000A1Z5";

        InvoiceResult created = await h.Invoices.CreateAsync(req, CancellationToken.None);
        await h.Invoices.PostAsync(created.InvoiceId, CancellationToken.None);

        var registers = await h.Db.SalesRegister
            .Where(r => r.SourceId == created.InvoiceId)
            .ToListAsync();

        var reg = Assert.Single(registers);
        Assert.Equal("B2B", reg.SupplyType);
        Assert.Equal(1000m, reg.TaxableAmount);
        Assert.Equal(90m, reg.CgstAmount);
        Assert.Equal(90m, reg.SgstAmount);
        Assert.Equal(1180m, reg.TotalAmount);
        Assert.Equal("33AAAAA0000A1Z5", reg.ContactGstin);
    }

    [SkippableFact]
    public async Task Attempting_to_edit_a_posted_invoice_is_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        InvoiceResult created = await h.Invoices.CreateAsync(
            Request(lines: [Line(quantity: 1m, unitPrice: 100m)]),
            CancellationToken.None);

        await h.Invoices.PostAsync(created.InvoiceId, CancellationToken.None);

        InvoiceResult editAttempt = await h.Invoices.UpdateAsync(
            created.InvoiceId,
            Request(lines: [Line(quantity: 2m, unitPrice: 200m)]),
            CancellationToken.None);

        Assert.Equal(InvoiceOutcome.LifecycleRefused, editAttempt.Outcome);
        Assert.Equal("Only draft invoices can be updated.", editAttempt.Detail);
    }

    [SkippableFact]
    public async Task Attempting_to_post_an_already_posted_invoice_is_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        InvoiceResult created = await h.Invoices.CreateAsync(
            Request(lines: [Line(quantity: 1m, unitPrice: 100m)]),
            CancellationToken.None);

        await h.Invoices.PostAsync(created.InvoiceId, CancellationToken.None);

        InvoiceResult postAgain = await h.Invoices.PostAsync(
            created.InvoiceId, CancellationToken.None);

        Assert.Equal(InvoiceOutcome.LifecycleRefused, postAgain.Outcome);
    }

    [SkippableFact]
    public async Task Gl_preview_returns_balanced_legs_without_persisting_or_posting()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        InvoiceResult created = await h.Invoices.CreateAsync(
            Request(lines: [Line(quantity: 3m, unitPrice: 300m)]),
            CancellationToken.None);

        GlPreviewResult? preview = await h.Invoices.PreviewGlAsync(created.InvoiceId, CancellationToken.None);

        Assert.NotNull(preview);
        Assert.True(preview.IsBalanced);
        Assert.Equal(preview.TotalDebit, preview.TotalCredit);
        Assert.Empty(h.Ledger.Posts);

        // State remains Draft
        Invoice invoice = await h.Db.Invoices.SingleAsync(i => i.InvoiceId == created.InvoiceId);
        Assert.Equal(DocumentStatus.Draft, invoice.Status);
    }

    [SkippableFact]
    public async Task Voiding_a_posted_invoice_withdraws_ledger_rows_and_cleans_sales_register()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        InvoiceResult created = await h.Invoices.CreateAsync(
            Request(lines: [Line(quantity: 1m, unitPrice: 1000m)]),
            CancellationToken.None);

        await h.Invoices.PostAsync(created.InvoiceId, CancellationToken.None);

        Assert.NotEmpty(await h.Db.SalesRegister.Where(r => r.SourceId == created.InvoiceId).ToListAsync());

        InvoiceResult voidResult = await h.Invoices.VoidAsync(
            created.InvoiceId,
            new VoidInvoiceRequest { Reason = "Entered wrong amount" },
            CancellationToken.None);

        Assert.Equal(InvoiceOutcome.Ok, voidResult.Outcome);

        // Ledger withdrawal requested
        PostLedgerRequest withdrawal = h.Ledger.Posts[^1];
        Assert.Empty(withdrawal.Legs);
        Assert.NotEmpty(withdrawal.WithdrawLedgerTypeIds);

        // Sales register entries removed
        Assert.Empty(await h.Db.SalesRegister.Where(r => r.SourceId == created.InvoiceId).ToListAsync());

        // Status updated to Void
        Invoice after = await h.Db.Invoices.SingleAsync(i => i.InvoiceId == created.InvoiceId);
        Assert.Equal(DocumentStatus.Void, after.Status);
        Assert.Equal("Entered wrong amount", after.VoidReason);
    }

    [SkippableFact]
    public async Task Voiding_is_blocked_if_downstream_credit_note_exists()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        (Guid customerId, Guid orgId) = h.Tenant.Require();

        InvoiceResult created = await h.Invoices.CreateAsync(
            Request(lines: [Line(quantity: 1m, unitPrice: 1000m)]),
            CancellationToken.None);

        await h.Invoices.PostAsync(created.InvoiceId, CancellationToken.None);

        // Add a downstream Credit Note referencing the invoice
        var creditNote = new CreditNote
        {
            OrgId = orgId,
            TransactionTypeCode = "CRN",
            DocumentNo = "CN/26/00001",
            DocumentDate = new DateOnly(2026, 6, 2),
            ContactId = ContactId,
            InvoiceId = created.InvoiceId,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Status = DocumentStatus.Posted,
            PostedAt = DateTimeOffset.UtcNow,
            PostedBy = Guid.NewGuid(),
            Lines = []
        };
        h.Db.CreditNotes.Add(creditNote);
        await h.Db.SaveChangesAsync();

        InvoiceResult voidResult = await h.Invoices.VoidAsync(
            created.InvoiceId,
            new VoidInvoiceRequest { Reason = "Attempted void" },
            CancellationToken.None);

        Assert.Equal(InvoiceOutcome.AlreadyCredited, voidResult.Outcome);
    }

    // =========================================================================
    // Helpers & Harness
    // =========================================================================

    private static SaveInvoiceRequest Request(List<SaveInvoiceLineRequest> lines) =>
        new()
        {
            DocumentDate = new DateOnly(2026, 6, 1),
            DueDate = new DateOnly(2026, 7, 1),
            ContactId = ContactId,
            PlaceOfSupplyStateCode = "33",
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Lines = lines,
        };

    private static SaveInvoiceLineRequest Line(decimal quantity, decimal unitPrice) =>
        new()
        {
            ItemId = ItemId,
            Quantity = quantity,
            ConversionFactor = 1m,
            UnitPrice = unitPrice,
            TaxGroupId = 1,
            LineType = DocumentLineType.Stock,
            Description = "Test Stock Item",
        };

    private sealed record Harness(
        SalesDbContext Db,
        InvoiceService Invoices,
        RecordingInventory Inventory,
        RecordingLedger Ledger,
        TenantContext Tenant)
    {
        public static async Task<Harness> CreateAsync(PostgresFixture pg)
        {
            Guid customerId = Guid.NewGuid();
            Guid orgId = Guid.NewGuid();

            TenantContext tenant = new() { CustomerId = customerId, OrgId = orgId };
            SalesDbContext db = pg.CreateContext(customerId, orgId);

            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
            await db.SaveChangesAsync();

            StubNameLookup names = new();
            RecordingInventory inventory = new();
            RecordingLedger ledger = new();
            StubTaxRates rates = new();
            StubBaseCurrency baseCurrency = new();
            StubBranchSettings branchSettings = new();
            StubCurrentUser currentUser = new();
            StubCreditCheck creditCheck = new();

            var numbering = new NumberGenerator(
                db, Options.Create(new NumberingOptions()), new StubFinancialYear());

            InvoiceService invoices = new(
                db,
                tenant,
                numbering,
                baseCurrency,
                branchSettings,
                rates,
                names,
                names,
                currentUser,
                TimeProvider.System,
                inventory,
                ledger,
                creditCheck);

            return new Harness(db, invoices, inventory, ledger, tenant);
        }
    }

    private sealed class RecordingInventory : IInventoryClient
    {
        public List<IssueStockRequest> Issues { get; } = [];

        public Task<ReserveStockResponse> ReserveAsync(ReserveStockRequest request, CancellationToken ct) =>
            Task.FromResult(new ReserveStockResponse { Success = true });

        public Task<IssueStockResponse> IssueAsync(IssueStockRequest request, CancellationToken ct)
        {
            Issues.Add(request);

            var response = new IssueStockResponse
            {
                Success = true,
                TotalValue = request.Lines.Sum(l => l.Quantity * 50m),
                Lines = request.Lines.Select(l => new IssueStockLineResult
                {
                    SourceLineId = l.SourceLineId,
                    ItemId = l.ItemId,
                    RequestedQuantity = l.Quantity,
                    Success = true,
                    Outcome = "Ok",
                    StockMovementId = 5555,
                    UnitCost = 50m,
                    LineValue = l.Quantity * 50m,
                }).ToList(),
            };

            return Task.FromResult(response);
        }

        public Task<ReleaseStockResponse> ReleaseAsync(ReleaseStockRequest request, CancellationToken ct) =>
            Task.FromResult(new ReleaseStockResponse { Success = true });

        /// <summary>Empty, as the real client answers when Inventory is unreachable.</summary>
        public Task<StockAvailabilityResponse> GetAvailabilityAsync(
            StockAvailabilityRequest request, CancellationToken ct) =>
            Task.FromResult(new StockAvailabilityResponse());

        public Task<ReceiveStockResponse> ReceiveAsync(ReceiveStockRequest request, CancellationToken ct) =>
            Task.FromResult(new ReceiveStockResponse { Success = true });
    }

    private sealed class RecordingLedger : ILedgerClient
    {
        public List<PostLedgerRequest> Posts { get; } = [];

        
        public Task<List<OutstandingBalanceView>> GetAllOutstandingBalancesAsync(int ledgerTypeId, CancellationToken ct) => Task.FromResult(new List<OutstandingBalanceView>());
        public Task<List<OutstandingBalanceView>> GetOutstandingBalancesAsync(long contactId, int ledgerTypeId, CancellationToken ct) => Task.FromResult(new List<OutstandingBalanceView>());
        public Task<PostLedgerOutcomeResult> PostAsync(PostLedgerRequest request, CancellationToken ct)
        {
            Posts.Add(request);
            return Task.FromResult(new PostLedgerOutcomeResult(true, null));
        }

        public Task<AllocateOutcomeResult> AllocateAsync(AllocateTransactionRequest request, CancellationToken ct) =>
            Task.FromResult(new AllocateOutcomeResult(true, null));

        /// <summary>
        /// Whatever a test wants the ledger to say a document has been settled
        /// for. Empty by default, which is the real answer for an invoice
        /// nothing has been received against.
        /// </summary>
        public Dictionary<long, Settlement> Settlements { get; } = [];

        public Task<IReadOnlyDictionary<long, Settlement>> GetSettlementsAsync(
            SettlementQueryRequest request, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<long, Settlement>>(
                request.TransactionIds
                    .Where(Settlements.ContainsKey)
                    .ToDictionary(id => id, id => Settlements[id]));

        public Task RemoveAllocationsAsync(RemoveAllocationsRequest request, CancellationToken ct) =>
            Task.CompletedTask;

        public decimal DebitOf(string accountSystemName) =>
            Posts.Count == 0
                ? 0m
                : Posts[^1].Legs
                    .Where(l => l.AccountSystemName == accountSystemName)
                    .Sum(l => l.DebitAmount);

        public decimal CreditOf(string accountSystemName) =>
            Posts.Count == 0
                ? 0m
                : Posts[^1].Legs
                    .Where(l => l.AccountSystemName == accountSystemName)
                    .Sum(l => l.CreditAmount);
    }

    private sealed class StubTaxRates : ITaxRateProvider
    {
        public Task<IReadOnlyDictionary<long, TaxRate>?> GetRatesAsync(DateOnly onDate, CancellationToken ct = default)
        {
            var rate = new TaxRate(1, 1, "GST 18%", 18m, 9m, 9m, 18m, 0m);
            IReadOnlyDictionary<long, TaxRate> dict = new Dictionary<long, TaxRate> { [1] = rate };
            return Task.FromResult<IReadOnlyDictionary<long, TaxRate>?>(dict);
        }

        public Task<TaxRate?> GetRateAsync(long taxGroupId, DateOnly onDate, CancellationToken ct = default) =>
            Task.FromResult<TaxRate?>(new TaxRate(1, taxGroupId, "GST 18%", 18m, 9m, 9m, 18m, 0m));
    }

    private sealed class StubBaseCurrency : IBaseCurrencyProvider
    {
        public Task<string?> GetBaseCurrencyAsync(CancellationToken ct) => Task.FromResult<string?>("INR");
    }

    private sealed class StubBranchSettings : IBranchSettingsProvider
    {
        public Task<BranchSettings?> GetSettingsAsync(CancellationToken ct) =>
            Task.FromResult<BranchSettings?>(new BranchSettings("33", true));
    }

    private sealed class StubNameLookup : IContactNameLookup, IItemNameLookup
    {
        public Task<IReadOnlyDictionary<long, NamedRef>> ResolveAsync(
            IReadOnlyCollection<long> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<long, NamedRef>>(
                ids.ToDictionary(id => id, id => new NamedRef(id, $"C{id}", $"Name {id}")));
    }

    private sealed class StubCurrentUser : ICurrentUser
    {
        public Guid? UserId => Guid.Parse("11111111-1111-1111-1111-111111111111");
        public Guid? CustomerId => Guid.Parse("22222222-2222-2222-2222-222222222222");
        public Guid? OrgId => Guid.Parse("33333333-3333-3333-3333-333333333333");
        public int? RoleId => 1;
    }

    private sealed class StubCreditCheck : ICreditCheckClient
    {
        public Task<CreditEvaluateResponse> EvaluateAsync(long contactId, decimal proposedAmountBase, CancellationToken ct) =>
            Task.FromResult(new CreditEvaluateResponse { Allowed = true });
    }

    private sealed class StubFinancialYear : IFinancialYearProvider
    {
        public Task<int> GetStartMonthAsync(CancellationToken ct = default) => Task.FromResult(4);
    }
}
