using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Purchase.Repository.Clients;
using Purchase.Repository.Repositories;
using Purchase.Entity.Enums;
using Purchase.Entity.Models;
using Purchase.Entity.TableEntities;
using Purchase.Repository;
using Shared.Kernel.Approvals;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Purchase.Api.Tests;

/// <summary>
/// End-to-End Purchase Pipeline Test Suite.
///
/// Rigorously validates:
/// 1. Test data setup with multiple vendors (Inter-State, Intra-State, Unregistered) and
///    multiple items (including identical descriptions across distinct SKUs).
/// 2. Multi-line transaction payload (at least 4 line items) handling common descriptions,
///    varying tax slabs, and decimal(18,4) precision.
/// 3. Document lifecycle matrix testing (Draft, Approved/Posted/Confirmed, Voided, Failed)
///    across POR, GRN, and BIL documents.
/// 4. Bulk bill generation across lifecycle statuses (Draft, ReadyToPost, Posted, Void).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PurchasePipelineTestSuite
{
    private const string InventoryAccount = "Inventory";
    private const string GrniAccount = "Goods Received Not Invoiced";
    private const string InputGstAccount = "Input GST";
    private const string AccountsPayableAccount = "Accounts Payable";

    // Test Contacts / Vendors
    private const long VendorA_InterStateId = 1001; // Karnataka (29)
    private const string VendorA_Gstin = "29ABCDE1234F1Z5";
    private const string VendorA_StateCode = "29";

    private const long VendorB_IntraStateId = 1002; // Tamil Nadu (33)
    private const string VendorB_Gstin = "33ABCDE1234F1Z5";
    private const string VendorB_StateCode = "33";

    private const long VendorC_UnregisteredId = 1003; // Unregistered / Composition
    private const string? VendorC_Gstin = null;
    private const string VendorC_StateCode = "33";

    // Test Items
    private const long Item1_Id = 2001;
    private const string Item1_Sku = "SKU-001";
    private const string Item1_Description = "Standard Steel Rods";

    private const long Item2_Id = 2002;
    private const string Item2_Sku = "SKU-002";
    private const string Item2_Description = "Standard Steel Rods"; // Same common description as Item 1

    private const long Item3_Id = 2003;
    private const string Item3_Sku = "SKU-003";
    private const string Item3_Description = "Heavy Duty Bolts";

    private const long Item4_Id = 2004;
    private const string Item4_Sku = "SKU-004";
    private const string Item4_Description = "Anchor Fasteners";

    private readonly PostgresFixture _pg;

    public PurchasePipelineTestSuite(PostgresFixture pg) => _pg = pg;

    // =========================================================================
    // SECTION 1 & 2: MULTI-LINE ITEM SCENARIOS & SHARED DESCRIPTION HANDLING
    // =========================================================================

    [SkippableFact]
    public async Task MultiLine_PurchaseOrder_SharedDescription_MaintainsRowIntegrity_AndDecimalPrecision()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        // 4-line purchase order payload:
        // Line 1 & Line 2 share exact same description: "Standard Steel Rods"
        // Line 3 & Line 4 distinct items with decimal(18,4) precision
        SavePurchaseOrderRequest request = new()
        {
            DocumentDate = new DateOnly(2026, 8, 1),
            ContactId = VendorA_InterStateId,
            ContactGstin = VendorA_Gstin,
            PlaceOfSupplyStateCode = VendorA_StateCode,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Lines =
            [
                new SavePurchaseOrderLineRequest
                {
                    ItemId = Item1_Id,
                    Description = Item1_Description,
                    Quantity = 10.5000m,
                    UnitPrice = 150.2500m,
                    TaxGroupId = 1, // 18%
                    LineType = DocumentLineType.Stock,
                },
                new SavePurchaseOrderLineRequest
                {
                    ItemId = Item2_Id,
                    Description = Item2_Description, // Same description string as Line 1
                    Quantity = 20.7500m,
                    UnitPrice = 145.5000m,
                    TaxGroupId = 1, // 18%
                    LineType = DocumentLineType.Stock,
                },
                new SavePurchaseOrderLineRequest
                {
                    ItemId = Item3_Id,
                    Description = Item3_Description,
                    Quantity = 15.1234m,
                    UnitPrice = 75.4321m,
                    TaxGroupId = 1, // 18%
                    LineType = DocumentLineType.Stock,
                },
                new SavePurchaseOrderLineRequest
                {
                    ItemId = Item4_Id,
                    Description = Item4_Description,
                    Quantity = 8.5555m,
                    UnitPrice = 50.1111m,
                    TaxGroupId = 2, // 12%
                    LineType = DocumentLineType.Stock,
                },
            ],
        };

        PurchaseOrderResult result = await h.Orders.CreateAsync(request, default);
        Assert.Equal(PurchaseOrderOutcome.Ok, result.Outcome);

        // Verify database storage in pur.PurchaseOrderDetails
        PurchaseOrder saved = await h.Db.PurchaseOrders
            .Include(po => po.Lines)
            .ThenInclude(l => l.Taxes)
            .SingleAsync(po => po.PurchaseOrderId == result.PurchaseOrderId);

        // 1. Assert exactly 4 rows created without collapsing
        Assert.Equal(4, saved.Lines.Count);

        // 2. Assert Line 1 & Line 2 share the same description string but have distinct LineNumbers and ItemIds
        PurchaseOrderDetail line1 = saved.Lines.Single(l => l.LineNumber == 1);
        PurchaseOrderDetail line2 = saved.Lines.Single(l => l.LineNumber == 2);
        PurchaseOrderDetail line3 = saved.Lines.Single(l => l.LineNumber == 3);
        PurchaseOrderDetail line4 = saved.Lines.Single(l => l.LineNumber == 4);

        Assert.Equal(Item1_Description, line1.Description);
        Assert.Equal(Item2_Description, line2.Description);
        Assert.Equal(Item1_Id, line1.ItemId);
        Assert.Equal(Item2_Id, line2.ItemId);
        Assert.NotEqual(line1.PurchaseOrderDetailId, line2.PurchaseOrderDetailId);
        Assert.Equal(10.5000m, line1.Quantity);
        Assert.Equal(150.2500m, line1.UnitPrice);
        Assert.Equal(20.7500m, line2.Quantity);
        Assert.Equal(145.5000m, line2.UnitPrice);

        // 3. Assert Line 3 & Line 4 preserve decimal(18,4) precision quantities
        Assert.Equal(15.1234m, line3.Quantity);
        Assert.Equal(75.4321m, line3.UnitPrice);
        Assert.Equal(8.5555m, line4.Quantity);
        Assert.Equal(50.1111m, line4.UnitPrice);

        // 4. Assert Inter-State taxation resolved: Vendor A is Karnataka (29) vs Branch TN (33) -> IGST only
        Assert.True(saved.IsInterState);
        Assert.Equal(0m, saved.CgstAmount);
        Assert.Equal(0m, saved.SgstAmount);
        Assert.True(saved.IgstAmount > 0m);
    }

    [SkippableFact]
    public async Task MultiLine_VendorBill_SharedDescription_MaintainsRowIntegrity_AndDecimalPrecision()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        SaveBillRequest request = new()
        {
            DocumentDate = new DateOnly(2026, 8, 5),
            VendorBillNo = "VB-SHARED-001",
            VendorBillDate = new DateOnly(2026, 8, 5),
            DueDate = new DateOnly(2026, 9, 5),
            ContactId = VendorB_IntraStateId,
            ContactGstin = VendorB_Gstin,
            PlaceOfSupplyStateCode = VendorB_StateCode,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Lines =
            [
                new SaveBillLineRequest
                {
                    ItemId = Item1_Id,
                    Description = Item1_Description,
                    Quantity = 12.3456m,
                    UnitPrice = 120.5000m,
                    TaxGroupId = 1,
                    LineType = DocumentLineType.Stock,
                },
                new SaveBillLineRequest
                {
                    ItemId = Item2_Id,
                    Description = Item2_Description, // Same description
                    Quantity = 24.6912m,
                    UnitPrice = 115.0000m,
                    TaxGroupId = 1,
                    LineType = DocumentLineType.Stock,
                },
                new SaveBillLineRequest
                {
                    ItemId = Item3_Id,
                    Description = Item3_Description,
                    Quantity = 9.8765m,
                    UnitPrice = 60.2500m,
                    TaxGroupId = 1,
                    LineType = DocumentLineType.Stock,
                },
                new SaveBillLineRequest
                {
                    ItemId = Item4_Id,
                    Description = Item4_Description,
                    Quantity = 5.4321m,
                    UnitPrice = 45.7500m,
                    TaxGroupId = 2,
                    LineType = DocumentLineType.Stock,
                },
            ],
        };

        BillResult result = await h.Bills.CreateAsync(request, default);
        Assert.Equal(BillOutcome.Ok, result.Outcome);

        Bill saved = await h.Db.Bills
            .Include(b => b.Lines)
            .ThenInclude(l => l.Taxes)
            .SingleAsync(b => b.BillId == result.BillId);

        // Verify 4 distinct lines saved in pur.VendorBillDetails
        Assert.Equal(4, saved.Lines.Count);

        BillDetail line1 = saved.Lines.Single(l => l.LineNumber == 1);
        BillDetail line2 = saved.Lines.Single(l => l.LineNumber == 2);
        BillDetail line3 = saved.Lines.Single(l => l.LineNumber == 3);
        BillDetail line4 = saved.Lines.Single(l => l.LineNumber == 4);

        Assert.Equal(Item1_Description, line1.Description);
        Assert.Equal(Item2_Description, line2.Description);
        Assert.Equal(Item1_Id, line1.ItemId);
        Assert.Equal(Item2_Id, line2.ItemId);
        Assert.NotEqual(line1.BillDetailId, line2.BillDetailId);
        Assert.Equal(12.3456m, line1.Quantity);
        Assert.Equal(24.6912m, line2.Quantity);
        Assert.Equal(9.8765m, line3.Quantity);
        Assert.Equal(5.4321m, line4.Quantity);

        // Intra-State: Vendor B is Tamil Nadu (33) -> CGST + SGST split, IGST = 0
        Assert.False(saved.IsInterState);
        Assert.True(saved.CgstAmount > 0m);
        Assert.True(saved.SgstAmount > 0m);
        Assert.Equal(0m, saved.IgstAmount);
    }

    // =========================================================================
    // SECTION 3: LIFECYCLE STATUS MATRIX TESTING (POR, GRN, BIL)
    // =========================================================================

    // --- Draft Status ---

    [SkippableFact]
    public async Task Lifecycle_Draft_PurchaseOrder_IsEditable_AndDoesNotTouchStockOrLedger()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        SavePurchaseOrderRequest create = CreatePoRequest(VendorB_IntraStateId, 10m, 100m);
        PurchaseOrderResult created = await h.Orders.CreateAsync(create, default);
        Assert.Equal(PurchaseOrderOutcome.Ok, created.Outcome);

        // Editable while Draft
        create.Lines[0].Quantity = 15m;
        PurchaseOrderResult updated = await h.Orders.UpdateAsync(created.PurchaseOrderId, create, default);
        Assert.Equal(PurchaseOrderOutcome.Ok, updated.Outcome);

        PurchaseOrder order = await h.Db.PurchaseOrders
            .Include(o => o.Lines)
            .SingleAsync(o => o.PurchaseOrderId == created.PurchaseOrderId);

        Assert.Equal(DocumentStatus.Draft, order.Status);
        Assert.Equal(15m, order.Lines.Single().Quantity);
        Assert.Null(order.PostedAt);
        Assert.Empty(h.Inventory.Calls);
        Assert.Empty(h.Ledger.Posts);
    }

    [SkippableFact]
    public async Task Lifecycle_Draft_VendorBill_IsEditable_AndDoesNotTouchStockOrLedger()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        SaveBillRequest create = CreateBillRequest(VendorB_IntraStateId, "VB-DRAFT-01", 10m, 100m);
        BillResult created = await h.Bills.CreateAsync(create, default);
        Assert.Equal(BillOutcome.Ok, created.Outcome);

        // Editable while Draft
        create.Lines[0].Quantity = 20m;
        BillResult updated = await h.Bills.UpdateAsync(created.BillId, create, default);
        Assert.Equal(BillOutcome.Ok, updated.Outcome);

        Bill bill = await h.Db.Bills
            .Include(b => b.Lines)
            .SingleAsync(b => b.BillId == created.BillId);

        Assert.Equal(DocumentStatus.Draft, bill.Status);
        Assert.Equal(20m, bill.Lines.Single().Quantity);
        Assert.Null(bill.PostedAt);
        Assert.Empty(h.Inventory.Calls);
        Assert.Empty(h.Ledger.Posts);
    }

    // --- Approved / Confirmed / Posted Status ---

    [SkippableFact]
    public async Task Lifecycle_Approved_PurchaseOrder_LocksAgainstEdits_AndAssignsNumbering()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        PurchaseOrderResult created = await h.Orders.CreateAsync(
            CreatePoRequest(VendorB_IntraStateId, 10m, 100m), default);

        // Confirm / Approve transitions Draft -> ReadyToPost / Confirmed
        PurchaseOrderResult confirmed = await h.Orders.ConfirmAsync(created.PurchaseOrderId, default);
        Assert.Equal(PurchaseOrderOutcome.Ok, confirmed.Outcome);

        PurchaseOrder order = await h.Db.PurchaseOrders.SingleAsync(o => o.PurchaseOrderId == created.PurchaseOrderId);
        Assert.Equal(DocumentStatus.ReadyToPost, order.Status);
        Assert.Equal(PurchaseFulfilmentStatus.Open, order.FulfilmentStatus);
        Assert.StartsWith("PO/", order.DocumentNo);

        // Direct edit is locked
        PurchaseOrderResult editRefused = await h.Orders.UpdateAsync(
            created.PurchaseOrderId, CreatePoRequest(VendorB_IntraStateId, 12m, 100m), default);
        Assert.Equal(PurchaseOrderOutcome.LifecycleRefused, editRefused.Outcome);
    }

    [SkippableFact]
    public async Task Lifecycle_Posted_GoodsReceipt_UpdatesStock_CreditsClearing_AndLocks()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        GoodsReceiptResult created = await h.Receipts.CreateAsync(
            new SaveGoodsReceiptRequest
            {
                DocumentDate = new DateOnly(2026, 8, 10),
                ContactId = VendorB_IntraStateId,
                PlaceOfSupplyStateCode = VendorB_StateCode,
                Lines =
                [
                    new SaveGoodsReceiptLineRequest
                    {
                        ItemId = Item1_Id,
                        Quantity = 10m,
                        AcceptedQuantity = 10m,
                        UnitPrice = 200m,
                        LineType = DocumentLineType.Stock,
                    },
                ],
            },
            default);

        Assert.Equal(GoodsReceiptOutcome.Ok, created.Outcome);

        GoodsReceiptResult posted = await h.Receipts.PostAsync(created.GoodsReceiptId, default);
        Assert.Equal(GoodsReceiptOutcome.Ok, posted.Outcome);

        // Stock received
        ReceiveStockRequest stockCall = Assert.Single(h.Inventory.Calls);
        Assert.Equal("GRN", stockCall.SourceType);
        Assert.Equal(created.GoodsReceiptId, stockCall.SourceId);
        Assert.Equal(10m, stockCall.Lines.Single().Quantity);

        // GRNI clearing credited
        Assert.Equal(2000m, h.Ledger.CreditOf(GrniAccount));

        // Direct edit locked
        GoodsReceiptResult editRefused = await h.Receipts.UpdateAsync(
            created.GoodsReceiptId,
            new SaveGoodsReceiptRequest
            {
                DocumentDate = new DateOnly(2026, 8, 10),
                ContactId = VendorB_IntraStateId,
                PlaceOfSupplyStateCode = VendorB_StateCode,
                Lines = [new SaveGoodsReceiptLineRequest { ItemId = Item1_Id, Quantity = 5m, AcceptedQuantity = 5m }],
            },
            default);
        Assert.Equal(GoodsReceiptOutcome.LifecycleRefused, editRefused.Outcome);
    }

    [SkippableFact]
    public async Task Lifecycle_Posted_VendorBill_Direct_MovesStock_GeneratesDoubleEntryLedger_AndLocks()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        SaveBillRequest request = CreateBillRequest(VendorB_IntraStateId, "VB-POST-01", 10m, 100m);
        BillResult created = await h.Bills.CreateAsync(request, default);

        BillResult posted = await h.Bills.PostAsync(created.BillId, default);
        Assert.Equal(BillOutcome.Ok, posted.Outcome);

        Bill bill = await h.Db.Bills.SingleAsync(b => b.BillId == created.BillId);
        Assert.Equal(DocumentStatus.Posted, bill.Status);
        Assert.NotNull(bill.PostedAt);
        Assert.StartsWith("BIL/", bill.DocumentNo);

        // Stock moved directly since no GRN
        ReceiveStockRequest stock = Assert.Single(h.Inventory.Calls);
        Assert.Equal("BIL", stock.SourceType);
        Assert.Equal(10m, stock.Lines.Single().Quantity);

        // Double-entry ledger generated and balanced
        PostLedgerRequest ledgerPost = Assert.Single(h.Ledger.Posts);
        Assert.Equal(ledgerPost.Legs.Sum(l => l.DebitAmount), ledgerPost.Legs.Sum(l => l.CreditAmount));
        Assert.Equal(1000m, h.Ledger.DebitOf(InventoryAccount));
        Assert.Equal(180m, h.Ledger.DebitOf(InputGstAccount)); // 18% Input GST
        Assert.Equal(1180m, h.Ledger.CreditOf(AccountsPayableAccount));

        // Direct edit is locked
        BillResult editRefused = await h.Bills.UpdateAsync(created.BillId, request, default);
        Assert.Equal(BillOutcome.LifecycleRefused, editRefused.Outcome);
    }

    // --- Voided Status ---

    [SkippableFact]
    public async Task Lifecycle_Voided_PurchaseOrder_CancelsFulfilment_AndMaintainsAudit()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        PurchaseOrderResult created = await h.Orders.CreateAsync(
            CreatePoRequest(VendorB_IntraStateId, 5m, 100m), default);
        await h.Orders.ConfirmAsync(created.PurchaseOrderId, default);

        PurchaseOrderResult voided = await h.Orders.VoidAsync(
            created.PurchaseOrderId, new VoidPurchaseOrderRequest { Reason = "Supplier cancelled item" }, default);
        Assert.Equal(PurchaseOrderOutcome.Ok, voided.Outcome);

        PurchaseOrder after = await h.Db.PurchaseOrders.SingleAsync(o => o.PurchaseOrderId == created.PurchaseOrderId);
        Assert.Equal(DocumentStatus.Void, after.Status);
        Assert.Equal(PurchaseFulfilmentStatus.Cancelled, after.FulfilmentStatus);
        Assert.Equal("Supplier cancelled item", after.VoidReason);
        Assert.NotNull(after.VoidedAt);
    }

    [SkippableFact]
    public async Task Lifecycle_Voided_VendorBill_WithdrawsLedgerEntries_AndMaintainsAudit()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        GoodsReceipt receipt = await h.PostedReceiptAsync(quantity: 4m, unitPrice: 100m);
        long receiptLineId = receipt.Lines.Single().GoodsReceiptDetailId;

        SaveBillRequest req = CreateBillRequest(VendorB_IntraStateId, "VB-VOID-01", 4m, 100m, receipt.GoodsReceiptId, receiptLineId);
        BillResult created = await h.Bills.CreateAsync(req, default);
        await h.Bills.PostAsync(created.BillId, default);

        BillResult voided = await h.Bills.VoidAsync(
            created.BillId, new VoidBillRequest { Reason = "Billed under incorrect GSTIN" }, default);
        Assert.Equal(BillOutcome.Ok, voided.Outcome);

        Bill bill = await h.Db.Bills.SingleAsync(b => b.BillId == created.BillId);
        Assert.Equal(DocumentStatus.Void, bill.Status);
        Assert.Equal("Billed under incorrect GSTIN", bill.VoidReason);
        Assert.NotNull(bill.VoidedAt);

        // Ledger withdrawal posting issued
        PostLedgerRequest withdrawal = h.Ledger.Posts[^1];
        Assert.Empty(withdrawal.Legs);
        Assert.NotEmpty(withdrawal.WithdrawLedgerTypeIds);
    }

    // --- Negative / Failure Scenarios ---

    [SkippableFact]
    public async Task Negative_VendorBill_DateAfterDocumentDate_IsCleanlyRefused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        SaveBillRequest req = CreateBillRequest(VendorB_IntraStateId, "VB-INVALID-DATE", 1m, 100m);
        // Impossible: Vendor dated bill in future after we allegedly received it
        req.DocumentDate = new DateOnly(2026, 8, 1);
        req.VendorBillDate = new DateOnly(2026, 8, 15);

        BillResult result = await h.Bills.CreateAsync(req, default);
        Assert.Equal(BillOutcome.LineInvalid, result.Outcome);
        Assert.Contains("cannot have dated their bill after you received it", result.Detail);

        // Assert no corrupt data saved
        Assert.Empty(await h.Db.Bills.ToListAsync());
    }

    [SkippableFact]
    public async Task Negative_CrossTenant_OrgId_QueryFilter_EnforcesTotalIsolation()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Guid customerId = Guid.NewGuid();
        Guid branch1 = Guid.NewGuid();
        Guid branch2 = Guid.NewGuid();

        await using PurchaseDbContext db1 = _pg.CreateContext(customerId, branch1);
        await using PurchaseDbContext db2 = _pg.CreateContext(customerId, branch2);

        db1.PurchaseOrders.Add(new PurchaseOrder
        {
            TransactionTypeCode = "POR",
            DocumentNo = "PO/TEST/ISOLATION",
            DocumentDate = new DateOnly(2026, 8, 1),
            ContactId = VendorB_IntraStateId,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Status = DocumentStatus.Draft,
        });
        await db1.SaveChangesAsync();

        // Branch 1 sees the row; Branch 2 query filter sees 0 rows
        Assert.NotEmpty(await db1.PurchaseOrders.Where(p => p.DocumentNo == "PO/TEST/ISOLATION").ToListAsync());
        Assert.Empty(await db2.PurchaseOrders.Where(p => p.DocumentNo == "PO/TEST/ISOLATION").ToListAsync());
    }

    [SkippableFact]
    public async Task Negative_ModifyingLockedDocument_ReturnsLifecycleRefused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        SaveBillRequest req = CreateBillRequest(VendorB_IntraStateId, "VB-LOCKED-01", 10m, 100m);
        BillResult created = await h.Bills.CreateAsync(req, default);
        await h.Bills.PostAsync(created.BillId, default);

        // Attempt modification of posted bill
        BillResult editResult = await h.Bills.UpdateAsync(created.BillId, req, default);
        Assert.Equal(BillOutcome.LifecycleRefused, editResult.Outcome);
        Assert.NotEmpty(editResult.Detail ?? string.Empty);
    }

    // =========================================================================
    // SECTION 4: BULK CREATION MATRIX (>= 45 BILLS ACROSS STATUSES)
    // =========================================================================

    [SkippableFact]
    public async Task Bulk_PurchaseBills_Creation_45Bills_AcrossAllLifecycleStatuses()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        // Generate 45 bills across:
        // - 15 Draft
        // - 10 ReadyToPost (Approved)
        // - 15 Posted
        // - 5 Void
        int totalBills = 45;
        List<long> createdIds = [];

        long[] vendors = [VendorA_InterStateId, VendorB_IntraStateId, VendorC_UnregisteredId];
        string?[] gstins = [VendorA_Gstin, VendorB_Gstin, VendorC_Gstin];
        string[] states = [VendorA_StateCode, VendorB_StateCode, VendorC_StateCode];

        for (int i = 1; i <= totalBills; i++)
        {
            int vendorIdx = i % 3;
            long vendorId = vendors[vendorIdx];
            string? gstin = gstins[vendorIdx];
            string state = states[vendorIdx];

            // Alternate items without common fallback descriptions
            long itemId = (i % 2 == 0) ? Item3_Id : Item4_Id;
            string description = (i % 2 == 0) ? $"Heavy Duty Bolts Grade {i}" : $"Anchor Fasteners {i}mm";

            SaveBillRequest billReq = new()
            {
                DocumentDate = new DateOnly(2026, 8, 1).AddDays(i % 28),
                VendorBillNo = $"VB-BULK-{i:D4}",
                VendorBillDate = new DateOnly(2026, 8, 1).AddDays(i % 28),
                DueDate = new DateOnly(2026, 9, 1).AddDays(i % 28),
                ContactId = vendorId,
                ContactGstin = gstin,
                PlaceOfSupplyStateCode = state,
                CurrencyCode = "INR",
                ExchangeRate = 1m,
                Lines =
                [
                    new SaveBillLineRequest
                    {
                        ItemId = itemId,
                        Description = description,
                        Quantity = 10m + (i * 0.5000m),
                        UnitPrice = 50m + (i * 1.2500m),
                        TaxGroupId = 1,
                        LineType = DocumentLineType.Stock,
                    },
                ],
            };

            BillResult created = await h.Bills.CreateAsync(billReq, default);
            Assert.Equal(BillOutcome.Ok, created.Outcome);
            createdIds.Add(created.BillId);

            // Lifecycle status assignments:
            if (i > 15 && i <= 25)
            {
                // ReadyToPost status (Approved)
                Bill b = await h.Db.Bills.SingleAsync(b => b.BillId == created.BillId);
                b.Status = DocumentStatus.ReadyToPost;
                await h.Db.SaveChangesAsync();
            }
            else if (i > 25 && i <= 40)
            {
                // Posted status
                BillResult posted = await h.Bills.PostAsync(created.BillId, default);
                Assert.Equal(BillOutcome.Ok, posted.Outcome);
            }
            else if (i > 40)
            {
                // Void status
                BillResult posted = await h.Bills.PostAsync(created.BillId, default);
                Assert.Equal(BillOutcome.Ok, posted.Outcome);
                BillResult voided = await h.Bills.VoidAsync(
                    created.BillId, new VoidBillRequest { Reason = $"Bulk test cancellation #{i}" }, default);
                Assert.Equal(BillOutcome.Ok, voided.Outcome);
            }
        }

        // Verify all 45 exist in DB with expected status distributions
        List<Bill> allBills = await h.Db.Bills.Where(b => createdIds.Contains(b.BillId)).ToListAsync();
        Assert.Equal(45, allBills.Count);

        int draftCount = allBills.Count(b => b.Status == DocumentStatus.Draft);
        int readyToPostCount = allBills.Count(b => b.Status == DocumentStatus.ReadyToPost);
        int postedCount = allBills.Count(b => b.Status == DocumentStatus.Posted);
        int voidCount = allBills.Count(b => b.Status == DocumentStatus.Void);

        Assert.Equal(15, draftCount);
        Assert.Equal(10, readyToPostCount);
        Assert.Equal(15, postedCount);
        Assert.Equal(5, voidCount);
    }

    // =========================================================================
    // FIXTURES & HELPERS
    // =========================================================================

    private static SavePurchaseOrderRequest CreatePoRequest(long vendorId, decimal quantity, decimal unitPrice) =>
        new()
        {
            DocumentDate = new DateOnly(2026, 8, 1),
            ContactId = vendorId,
            PlaceOfSupplyStateCode = "33",
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Lines =
            [
                new SavePurchaseOrderLineRequest
                {
                    ItemId = Item1_Id,
                    Description = Item1_Description,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    TaxGroupId = 1,
                    LineType = DocumentLineType.Stock,
                },
            ],
        };

    private static SaveBillRequest CreateBillRequest(
        long vendorId,
        string billNo,
        decimal quantity,
        decimal unitPrice,
        long? receiptId = null,
        long? receiptDetailId = null) =>
        new()
        {
            DocumentDate = new DateOnly(2026, 8, 1),
            VendorBillNo = billNo,
            VendorBillDate = new DateOnly(2026, 8, 1),
            DueDate = new DateOnly(2026, 9, 1),
            ContactId = vendorId,
            PlaceOfSupplyStateCode = "33",
            GoodsReceiptId = receiptId,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Lines =
            [
                new SaveBillLineRequest
                {
                    ItemId = Item1_Id,
                    Description = Item1_Description,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    TaxGroupId = 1,
                    GoodsReceiptDetailId = receiptDetailId,
                    LineType = DocumentLineType.Stock,
                },
            ],
        };

    private sealed record Harness(
        PurchaseDbContext Db,
        PurchaseOrderRepository Orders,
        BillRepository Bills,
        GoodsReceiptRepository Receipts,
        RecordingInventory Inventory,
        RecordingLedger Ledger,
        StubPaymentTerms PaymentTerms,
        RecordingFixedAssets FixedAssets)
    {
        public static async Task<Harness> CreateAsync(PostgresFixture pg)
        {
            Guid customerId = Guid.NewGuid();
            Guid orgId = Guid.NewGuid();

            TenantContext tenant = new() { CustomerId = customerId, OrgId = orgId };
            PurchaseDbContext db = pg.CreateContext(customerId, orgId);

            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
            await db.SaveChangesAsync();

            StubNameLookup names = new();
            RecordingInventory inventory = new();
            RecordingLedger ledger = new();
            StubPaymentTerms terms = new();
            RecordingFixedAssets fixedAssets = new();

            var numbering = new NumberGenerator(
                db, Options.Create(new NumberingOptions()), new StubFinancialYear());

            PurchaseOrderRepository orders = new(
                db, numbering, new StubBaseCurrency(), new StubBranchSettings("33"),
                new StubTaxRates(), names, names, new StubCurrentUser(), TimeProvider.System);

            GoodsReceiptRepository receipts = new(
                db, tenant, numbering, new StubBaseCurrency(), new StubBranchSettings("33"),
                new StubTaxRates(), names, names, inventory, ledger,
                new StubCurrentUser(), TimeProvider.System);

            BillRepository bills = new(
                db, tenant, numbering, new StubBaseCurrency(), new StubBranchSettings("33"),
                new StubTaxRates(), names, names, inventory, ledger, terms, fixedAssets,
                new StubCurrentUser(), TimeProvider.System);

            return new Harness(db, orders, bills, receipts, inventory, ledger, terms, fixedAssets);
        }

        public async Task<GoodsReceipt> PostedReceiptAsync(decimal quantity, decimal unitPrice)
        {
            GoodsReceiptResult created = await Receipts.CreateAsync(
                new SaveGoodsReceiptRequest
                {
                    DocumentDate = new DateOnly(2026, 8, 1),
                    ContactId = VendorB_IntraStateId,
                    PlaceOfSupplyStateCode = "33",
                    Lines =
                    [
                        new SaveGoodsReceiptLineRequest
                        {
                            ItemId = Item1_Id,
                            Quantity = quantity,
                            AcceptedQuantity = quantity,
                            RejectedQuantity = 0m,
                            UnitPrice = unitPrice,
                            LineType = DocumentLineType.Stock,
                        },
                    ],
                },
                default);

            await Receipts.PostAsync(created.GoodsReceiptId, default);

            return await Db.GoodsReceipts
                .Include(r => r.Lines)
                .SingleAsync(r => r.GoodsReceiptId == created.GoodsReceiptId);
        }
    }
}
