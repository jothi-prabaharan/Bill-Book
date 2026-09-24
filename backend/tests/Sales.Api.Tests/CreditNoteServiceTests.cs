using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sales.Api.Controllers;
using Sales.Api.Services;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Internal;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// The credit note — save, post and void — against a real PostgreSQL, the same
/// discipline <c>SalesOrderServiceTests</c> uses.
///
/// <b>The save path's two bugs</b> are the ones <c>DeliveryChallanServiceTests</c>
/// covers: no <c>BaseQuantity</c> or <c>LineNumber</c>, and a tax split that
/// never branched on <c>IsInterState</c>.
///
/// <b>What TK-13 found in post and void.</b> Returned goods went back at the
/// <i>selling</i> price and as a plain receipt, so the id naming the issue they
/// came from was stored and never read. The ledger legs named "Sales Returns"
/// and "Tax Payable" — one unseeded, one in no chart — so every post was
/// refused, which the stub ledger here could never have noticed. And the void
/// took no reason while the database requires one beside the timestamp, so
/// every void failed.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class CreditNoteServiceTests
{
    private readonly PostgresFixture _pg;

    public CreditNoteServiceTests(PostgresFixture pg) => _pg = pg;

    // ── Save ────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task An_intra_state_credit_note_saves_with_cgst_and_sgst_and_a_real_base_quantity()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        InvoiceDetail invoiceLine = await h.SeedInvoiceLineAsync();

        long id = await h.SaveOkAsync(
            Request(h.InvoiceId, invoiceLine.InvoiceDetailId, "33AAAAA0000A1Z5", [Line(4m, 100m)]));

        CreditNote saved = await h.Db.CreditNotes
            .Include(x => x.Lines).ThenInclude(l => l.Taxes)
            .SingleAsync(x => x.CreditNoteId == id);

        Assert.False(saved.IsInterState);

        // 4 × 100 = 400 taxable, 18% = 72 split 36 CGST / 36 SGST, total 472.
        Assert.Equal(400m, saved.TaxableAmount);
        Assert.Equal(36m, saved.CgstAmount);
        Assert.Equal(36m, saved.SgstAmount);
        Assert.Equal(0m, saved.IgstAmount);
        Assert.Equal(472m, saved.TotalAmount);

        CreditNoteDetail line = saved.Lines.Single();
        Assert.Equal(1, line.LineNumber);
        Assert.Equal(4m, line.BaseQuantity);
        Assert.Equal(400m, line.GrossAmount);
        Assert.Equal(400m, line.TaxableAmount);
        Assert.Equal(invoiceLine.InvoiceDetailId, line.InvoiceDetailId);

        Assert.Equal(2, line.Taxes.Count);
        Assert.Contains(line.Taxes, t => t.TaxComponent == TaxComponent.Cgst && t.Amount == 36m);
        Assert.Contains(line.Taxes, t => t.TaxComponent == TaxComponent.Sgst && t.Amount == 36m);
    }

    [SkippableFact]
    public async Task An_inter_state_credit_note_saves_with_igst_only()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        InvoiceDetail invoiceLine = await h.SeedInvoiceLineAsync();

        // 07 is Delhi; the branch is seeded at 33 (Tamil Nadu) — see StubBranchSettings.
        long id = await h.SaveOkAsync(
            Request(h.InvoiceId, invoiceLine.InvoiceDetailId, "07AAAAA0000A1Z5", [Line(2m, 250m)]));

        CreditNote saved = await h.Db.CreditNotes
            .Include(x => x.Lines).ThenInclude(l => l.Taxes)
            .SingleAsync(x => x.CreditNoteId == id);

        Assert.True(saved.IsInterState);
        Assert.Equal(0m, saved.CgstAmount);
        Assert.Equal(0m, saved.SgstAmount);
        Assert.Equal(90m, saved.IgstAmount); // 500 taxable × 18%
        Assert.Equal(590m, saved.TotalAmount);

        CreditNoteDetailTax tax = Assert.Single(saved.Lines.Single().Taxes);
        Assert.Equal(TaxComponent.Igst, tax.TaxComponent);
        Assert.Equal(90m, tax.Amount);
    }

    [SkippableFact]
    public async Task A_line_that_is_not_on_the_invoice_is_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        await h.SeedInvoiceLineAsync();

        CreditNoteResult result = await h.Service.SaveAsync(
            null, Request(h.InvoiceId, long.MaxValue, "33AAAAA0000A1Z5", [Line(1m, 100m)]), default);

        Assert.Equal(CreditNoteOutcome.LineInvalid, result.Outcome);
        Assert.Equal(0, await h.Db.CreditNotes.CountAsync());
    }

    [SkippableFact]
    public async Task A_line_naming_another_item_than_its_invoice_line_is_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        InvoiceDetail invoiceLine = await h.SeedInvoiceLineAsync();

        SaveCreditNoteRequest request = Request(
            h.InvoiceId, invoiceLine.InvoiceDetailId, "33AAAAA0000A1Z5", [Line(1m, 100m)]);
        request.Lines[0].ItemId = 8;

        CreditNoteResult result = await h.Service.SaveAsync(null, request, default);

        Assert.Equal(CreditNoteOutcome.LineInvalid, result.Outcome);
    }

    [SkippableFact]
    public async Task A_credit_note_against_an_unposted_invoice_is_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg, invoiceStatus: DocumentStatus.Draft);
        InvoiceDetail invoiceLine = await h.SeedInvoiceLineAsync();

        CreditNoteResult result = await h.Service.SaveAsync(
            null, Request(h.InvoiceId, invoiceLine.InvoiceDetailId, "33AAAAA0000A1Z5", [Line(1m, 100m)]), default);

        Assert.Equal(CreditNoteOutcome.SourceInvalid, result.Outcome);
    }

    [SkippableFact]
    public async Task A_credit_note_for_another_customer_than_its_invoice_is_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        InvoiceDetail invoiceLine = await h.SeedInvoiceLineAsync();

        SaveCreditNoteRequest request = Request(
            h.InvoiceId, invoiceLine.InvoiceDetailId, "33AAAAA0000A1Z5", [Line(1m, 100m)]);
        request.ContactId = 43;

        CreditNoteResult result = await h.Service.SaveAsync(null, request, default);

        Assert.Equal(CreditNoteOutcome.SourceInvalid, result.Outcome);
    }

    // ── Post ────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Posting_reverses_the_revenue_and_the_gst_into_accounts_the_chart_has()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        InvoiceDetail invoiceLine = await h.SeedInvoiceLineAsync();

        long id = await h.SaveOkAsync(
            Request(h.InvoiceId, invoiceLine.InvoiceDetailId, "33AAAAA0000A1Z5", [Line(4m, 100m)]));

        CreditNoteResult posted = await h.Service.PostAsync(id, default);
        Assert.Equal(CreditNoteOutcome.Ok, posted.Outcome);

        PostLedgerRequest post = Assert.Single(h.Ledger.Posts);
        Assert.Equal("CRN", post.TransactionTypeCode);
        Assert.Equal(id, post.TransactionId);

        LedgerLegRequest receivable = Assert.Single(post.Legs, l => l.AccountSystemName == "Accounts Receivable");
        Assert.Equal(472m, receivable.CreditAmount);
        Assert.Equal(42, receivable.SubAccountReferenceId);

        LedgerLegRequest returns = Assert.Single(post.Legs, l => l.AccountSystemName == "Sales Returns");
        Assert.Equal(400m, returns.DebitAmount);

        List<LedgerLegRequest> gst = post.Legs.Where(l => l.AccountSystemName == "Output GST").ToList();
        Assert.Equal(2, gst.Count);
        Assert.Contains(gst, l => l.SubAccountTaxComponent == 1 && l.DebitAmount == 36m); // CGST
        Assert.Contains(gst, l => l.SubAccountTaxComponent == 2 && l.DebitAmount == 36m); // SGST
        Assert.All(gst, l => Assert.Equal(1, l.SubAccountReferenceId)); // the tax group

        // Balanced, and no stock legs: Inventory's worker posts the return.
        Assert.Equal(post.Legs.Sum(l => l.DebitAmount), post.Legs.Sum(l => l.CreditAmount));
        Assert.DoesNotContain(post.Legs, l => l.AccountSystemName is "Inventory" or "Cost of Goods Sold");

        // Claimed against the invoice, once.
        AllocateTransactionRequest claim = Assert.Single(h.Ledger.Allocations);
        Assert.Equal(h.InvoiceId, claim.TargetTransactionId);
        Assert.Equal(472m, claim.Amount);

        h.Db.ChangeTracker.Clear();
        CreditNote saved = await h.Db.CreditNotes.SingleAsync(x => x.CreditNoteId == id);
        Assert.Equal(DocumentStatus.Posted, saved.Status);
        Assert.Equal(1, await h.Db.SalesRegister.CountAsync(r => r.SourceId == id && r.TransactionTypeCode == "CRN"));
    }

    [SkippableFact]
    public async Task A_sales_return_comes_back_at_the_invoice_lines_cost_against_its_issue()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        // Sold at 100, cost 60, issued on movement 9001.
        InvoiceDetail invoiceLine = await h.SeedInvoiceLineAsync(unitCost: 60m, stockMovementId: 9001);

        long id = await h.SaveOkAsync(
            Request(h.InvoiceId, invoiceLine.InvoiceDetailId, "33AAAAA0000A1Z5", [Line(4m, 100m)]));
        Assert.Equal(CreditNoteOutcome.Ok, (await h.Service.PostAsync(id, default)).Outcome);

        ReceiveStockRequest receipt = Assert.Single(h.Inventory.Receipts);
        Assert.Equal("CRN", receipt.SourceType);
        Assert.Equal(id, receipt.SourceId);

        ReceiveStockLine returned = Assert.Single(receipt.Lines);
        Assert.Equal(7, returned.ItemId);
        Assert.Equal(4m, returned.Quantity);

        // The cost it went out at, not the 100 it was sold for — and the issue
        // it reverses, so Inventory records a sales return onto its layers.
        Assert.Equal(60m, returned.UnitCost);
        Assert.Equal(9001, returned.ReturnsStockMovementId);

        h.Db.ChangeTracker.Clear();
        InvoiceDetail after = await h.Db.InvoiceDetails.SingleAsync(l => l.InvoiceDetailId == invoiceLine.InvoiceDetailId);
        Assert.Equal(4m, after.ReturnedQuantity);
    }

    [SkippableFact]
    public async Task A_price_correction_returns_no_stock()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        InvoiceDetail invoiceLine = await h.SeedInvoiceLineAsync(unitCost: 60m, stockMovementId: 9001);

        SaveCreditNoteRequest request = Request(
            h.InvoiceId, invoiceLine.InvoiceDetailId, "33AAAAA0000A1Z5", [Line(10m, 5m)]);
        request.ReasonCode = CreditNoteReason.PriceCorrection;

        long id = await h.SaveOkAsync(request);
        Assert.Equal(CreditNoteOutcome.Ok, (await h.Service.PostAsync(id, default)).Outcome);

        Assert.Empty(h.Inventory.Receipts);

        h.Db.ChangeTracker.Clear();
        InvoiceDetail after = await h.Db.InvoiceDetails.SingleAsync(l => l.InvoiceDetailId == invoiceLine.InvoiceDetailId);
        Assert.Equal(0m, after.ReturnedQuantity);
    }

    [SkippableFact]
    public async Task More_cannot_come_back_than_the_invoice_line_has_left()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        InvoiceDetail invoiceLine = await h.SeedInvoiceLineAsync(returnedQuantity: 8m); // 10 sold, 8 back already

        long id = await h.SaveOkAsync(
            Request(h.InvoiceId, invoiceLine.InvoiceDetailId, "33AAAAA0000A1Z5", [Line(3m, 100m)]));

        CreditNoteResult result = await h.Service.PostAsync(id, default);

        Assert.Equal(CreditNoteOutcome.OverReturned, result.Outcome);
        Assert.Contains("2 left", result.Detail);
        Assert.Empty(h.Ledger.Allocations);
        Assert.Empty(h.Inventory.Receipts);
        Assert.Empty(h.Ledger.Posts);
    }

    [SkippableFact]
    public async Task A_refused_claim_leaves_the_note_a_draft_with_nothing_moved()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        InvoiceDetail invoiceLine = await h.SeedInvoiceLineAsync();

        long id = await h.SaveOkAsync(
            Request(h.InvoiceId, invoiceLine.InvoiceDetailId, "33AAAAA0000A1Z5", [Line(4m, 100m)]));
        h.Ledger.RefuseAllocationWith = "The invoice has 100.00 left to settle.";

        CreditNoteResult result = await h.Service.PostAsync(id, default);

        Assert.Equal(CreditNoteOutcome.AllocationRefused, result.Outcome);
        Assert.Contains("100.00 left", result.Detail);
        Assert.Empty(h.Inventory.Receipts);
        Assert.Empty(h.Ledger.Posts);

        h.Db.ChangeTracker.Clear();
        Assert.Equal(DocumentStatus.Draft, (await h.Db.CreditNotes.SingleAsync(x => x.CreditNoteId == id)).Status);
    }

    [SkippableFact]
    public async Task A_refused_posting_releases_the_claim_it_took()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        InvoiceDetail invoiceLine = await h.SeedInvoiceLineAsync();

        long id = await h.SaveOkAsync(
            Request(h.InvoiceId, invoiceLine.InvoiceDetailId, "33AAAAA0000A1Z5", [Line(4m, 100m)]));
        h.Ledger.RefusePostWith = "The chart of accounts has no 'Sales Returns'.";

        CreditNoteResult result = await h.Service.PostAsync(id, default);

        Assert.Equal(CreditNoteOutcome.PostingRefused, result.Outcome);
        RemoveAllocationsRequest released = Assert.Single(h.Ledger.RemovedAllocations);
        Assert.Equal(id, released.SourceTransactionId);

        h.Db.ChangeTracker.Clear();
        Assert.Equal(DocumentStatus.Draft, (await h.Db.CreditNotes.SingleAsync(x => x.CreditNoteId == id)).Status);
        InvoiceDetail after = await h.Db.InvoiceDetails.SingleAsync(l => l.InvoiceDetailId == invoiceLine.InvoiceDetailId);
        Assert.Equal(0m, after.ReturnedQuantity);
    }

    [SkippableFact]
    public async Task A_rounded_total_posts_its_rounding_and_still_balances()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        InvoiceDetail invoiceLine = await h.SeedInvoiceLineAsync();

        // 3 × 33.33 = 99.99, + 9.00 CGST + 9.00 SGST = 117.99 → rounds to 118.
        long id = await h.SaveOkAsync(
            Request(h.InvoiceId, invoiceLine.InvoiceDetailId, "33AAAAA0000A1Z5", [Line(3m, 33.33m)]));
        Assert.Equal(CreditNoteOutcome.Ok, (await h.Service.PostAsync(id, default)).Outcome);

        PostLedgerRequest post = Assert.Single(h.Ledger.Posts);
        Assert.Single(post.Legs, l => l.AccountSystemName == "Round Off");
        Assert.Equal(post.Legs.Sum(l => l.DebitAmount), post.Legs.Sum(l => l.CreditAmount));
    }

    // ── Void ────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Voiding_a_draft_keeps_the_reason_the_database_requires()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        InvoiceDetail invoiceLine = await h.SeedInvoiceLineAsync();

        long id = await h.SaveOkAsync(
            Request(h.InvoiceId, invoiceLine.InvoiceDetailId, "33AAAAA0000A1Z5", [Line(4m, 100m)]));

        CreditNoteResult result = await h.Service.VoidAsync(id, "  Raised twice  ", default);
        Assert.Equal(CreditNoteOutcome.Ok, result.Outcome);

        h.Db.ChangeTracker.Clear();
        CreditNote saved = await h.Db.CreditNotes.SingleAsync(x => x.CreditNoteId == id);
        Assert.Equal(DocumentStatus.Void, saved.Status);
        Assert.Equal("Raised twice", saved.VoidReason);
        Assert.NotNull(saved.VoidedAt);
        Assert.Empty(h.Ledger.Posts); // a draft posted nothing to withdraw
    }

    [SkippableFact]
    public async Task A_void_without_a_reason_is_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        InvoiceDetail invoiceLine = await h.SeedInvoiceLineAsync();

        long id = await h.SaveOkAsync(
            Request(h.InvoiceId, invoiceLine.InvoiceDetailId, "33AAAAA0000A1Z5", [Line(4m, 100m)]));

        CreditNoteResult result = await h.Service.VoidAsync(id, "   ", default);

        Assert.Equal(CreditNoteOutcome.LifecycleRefused, result.Outcome);
    }

    [Fact]
    public void A_void_request_without_a_reason_fails_model_validation_so_the_api_answers_400()
    {
        var request = new VoidCreditNoteRequest { Reason = null! };
        var results = new List<ValidationResult>();

        bool valid = Validator.TryValidateObject(request, new ValidationContext(request), results, true);

        Assert.False(valid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(VoidCreditNoteRequest.Reason)));
    }

    [SkippableFact]
    public async Task Voiding_a_posted_price_correction_withdraws_its_entry_and_releases_its_claim()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        InvoiceDetail invoiceLine = await h.SeedInvoiceLineAsync();

        SaveCreditNoteRequest request = Request(
            h.InvoiceId, invoiceLine.InvoiceDetailId, "33AAAAA0000A1Z5", [Line(10m, 5m)]);
        request.ReasonCode = CreditNoteReason.PriceCorrection;

        long id = await h.SaveOkAsync(request);
        await h.Service.PostAsync(id, default);

        CreditNoteResult result = await h.Service.VoidAsync(id, "Priced correctly after all", default);
        Assert.Equal(CreditNoteOutcome.Ok, result.Outcome);

        PostLedgerRequest withdrawal = h.Ledger.Posts.Last();
        Assert.Empty(withdrawal.Legs);
        Assert.Equal(id, withdrawal.TransactionId);
        Assert.Contains(3, withdrawal.WithdrawLedgerTypeIds); // CONTROL
        Assert.Contains(1, withdrawal.WithdrawLedgerTypeIds); // ITEM
        Assert.Contains(2, withdrawal.WithdrawLedgerTypeIds); // TAX

        Assert.Single(h.Ledger.RemovedAllocations);
        Assert.Equal(0, await h.Db.SalesRegister.CountAsync(r => r.SourceId == id && r.TransactionTypeCode == "CRN"));
    }

    [SkippableFact]
    public async Task Voiding_a_posted_sales_return_is_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        InvoiceDetail invoiceLine = await h.SeedInvoiceLineAsync(unitCost: 60m, stockMovementId: 9001);

        long id = await h.SaveOkAsync(
            Request(h.InvoiceId, invoiceLine.InvoiceDetailId, "33AAAAA0000A1Z5", [Line(4m, 100m)]));
        await h.Service.PostAsync(id, default);

        CreditNoteResult result = await h.Service.VoidAsync(id, "Wrong invoice", default);

        Assert.Equal(CreditNoteOutcome.LifecycleRefused, result.Outcome);
        Assert.Contains("back in stock", result.Detail);

        h.Db.ChangeTracker.Clear();
        Assert.Equal(DocumentStatus.Posted, (await h.Db.CreditNotes.SingleAsync(x => x.CreditNoteId == id)).Status);
    }

    // ── Controller ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(nameof(CreditNotesController.Post), "approve")]
    [InlineData(nameof(CreditNotesController.Void), "void")]
    public void Post_and_void_need_their_own_permissions_not_edit(string action, string permission)
    {
        MethodInfo method = typeof(CreditNotesController).GetMethod(action)!;

        PermissionActionAttribute attribute = Assert.Single(method.GetCustomAttributes<PermissionActionAttribute>());
        Assert.Equal(permission, attribute.Action);
    }

    [Fact]
    public void Every_id_route_is_constrained_to_a_number()
    {
        IEnumerable<string> templates = typeof(CreditNotesController).GetMethods()
            .SelectMany(m => m.GetCustomAttributes<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>())
            .Select(a => a.Template)
            .OfType<string>()
            .Where(t => t.Contains("{id"));

        Assert.All(templates, t => Assert.Contains("{id:long}", t));
    }

    [SkippableFact]
    public async Task Another_branchs_credit_note_is_not_found()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness mine = await Harness.CreateAsync(_pg);
        Harness theirs = await Harness.CreateAsync(_pg);
        InvoiceDetail invoiceLine = await theirs.SeedInvoiceLineAsync();

        long id = await theirs.SaveOkAsync(
            Request(theirs.InvoiceId, invoiceLine.InvoiceDetailId, "33AAAAA0000A1Z5", [Line(1m, 100m)]));

        var controller = new CreditNotesController(mine.Service);

        // 404, not 403: row-level security hides it from this service (TK-02).
        Assert.IsType<NotFoundResult>(await controller.Get(id, default));
        Assert.IsType<NotFoundResult>(await controller.Post(id, default));
        Assert.IsType<NotFoundResult>(
            await controller.Void(id, new VoidCreditNoteRequest { Reason = "Not ours" }, default));
        Assert.Empty(mine.Ledger.Allocations);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static SaveCreditNoteRequest Request(
        long invoiceId,
        long invoiceDetailId,
        string contactGstin,
        List<SaveCreditNoteLineRequest> lines)
    {
        foreach (SaveCreditNoteLineRequest line in lines)
        {
            line.InvoiceDetailId = invoiceDetailId;
        }

        return new SaveCreditNoteRequest
        {
            InvoiceId = invoiceId,
            DocumentDate = new DateOnly(2026, 6, 1),
            ContactId = 42,
            ContactGstin = contactGstin,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            ReasonCode = CreditNoteReason.SalesReturn,
            Lines = lines,
        };
    }

    private static SaveCreditNoteLineRequest Line(decimal quantity, decimal unitPrice) =>
        new()
        {
            ItemId = 7,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TaxGroupId = 1,
        };

    /// <summary>
    /// One branch, a posted invoice with a line to credit against — <see
    /// cref="CreditNoteDetail.InvoiceDetailId"/> is a real foreign key — and the
    /// service wired to stubs that record what it asked for.
    /// </summary>
    private sealed record Harness(
        SalesDbContext Db,
        CreditNoteService Service,
        long InvoiceId,
        RecordingInventory Inventory,
        RecordingLedger Ledger)
    {
        public static async Task<Harness> CreateAsync(
            PostgresFixture pg, DocumentStatus invoiceStatus = DocumentStatus.Posted)
        {
            Guid customerId = Guid.NewGuid();
            Guid orgId = Guid.NewGuid();

            SalesDbContext db = pg.CreateContext(customerId, orgId);

            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));

            Invoice invoice = new()
            {
                TransactionTypeCode = "INV",
                DocumentNo = $"INV/{Guid.NewGuid():N}"[..20],
                DocumentDate = new DateOnly(2026, 6, 1),
                DueDate = new DateOnly(2026, 6, 30),
                ContactId = 42,
                CurrencyCode = "INR",
                ExchangeRate = 1m,
                Status = invoiceStatus,
                PostedAt = invoiceStatus == DocumentStatus.Posted ? DateTimeOffset.UtcNow : null,
            };
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();

            StubNameLookup names = new();
            RecordingInventory inventory = new();
            RecordingLedger ledger = new();
            NumberGenerator numbering = new(
                db, Options.Create(new NumberingOptions()), new StubFinancialYear());

            CreditNoteService service = new(
                db,
                new TenantContext { CustomerId = customerId, OrgId = orgId, CustomerCode = "0000000042" },
                numbering,
                new StubBaseCurrency(),
                new StubBranchSettings(),
                new StubTaxRates(),
                names,
                names,
                new StubCurrentUser(),
                TimeProvider.System,
                inventory,
                ledger);

            return new Harness(db, service, invoice.InvoiceId, inventory, ledger);
        }

        public async Task<long> SaveOkAsync(SaveCreditNoteRequest request)
        {
            CreditNoteResult result = await Service.SaveAsync(null, request, default);
            Assert.True(
                result.Outcome == CreditNoteOutcome.Ok,
                $"Save refused: {result.Outcome} — {result.Detail}");
            return result.CreditNoteId;
        }

        /// <summary>
        /// The invoice line the credit note reverses — 10 of item 7 at 100 —
        /// built the same minimal way <c>DocumentLineFieldTests</c> does, since
        /// the invoice's own save path is not what these tests are about.
        /// </summary>
        public async Task<InvoiceDetail> SeedInvoiceLineAsync(
            decimal unitCost = 0m, long? stockMovementId = null, decimal returnedQuantity = 0m)
        {
            InvoiceDetail line = new()
            {
                InvoiceId = InvoiceId,
                LineNumber = 1,
                ItemId = 7,
                Quantity = 10m,
                ConversionFactor = 1m,
                BaseQuantity = 10m,
                UnitPrice = 100m,
                DiscountPercent = 0m,
                DiscountAmount = 0m,
                TaxableAmount = 1000m,
                GrossAmount = 1000m,
                LineTotal = 1000m,
                LineType = DocumentLineType.Stock,
                UnitCost = unitCost,
                StockMovementId = stockMovementId,
                ReturnedQuantity = returnedQuantity,
            };

            Db.InvoiceDetails.Add(line);
            await Db.SaveChangesAsync();
            return line;
        }
    }
}
