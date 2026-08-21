# Backend Domain Models & Database Context Survey: Stage T3.1 — Invoices

## 1. Executive Summary & Problem Boundary

This investigation surveys the backend domain models, EF Core database context, Row-Level Security (RLS) policies, numbering series generator, GST calculation engine, and ledger posting integration required for **Stage T3.1 — Invoices** in the Bill-Book ERP SaaS application.

### Key Architectural Tenets (from `AGENTS.md` and `CLAUDE.md`)
1. **LINQ / EF Core Only**: No raw SQL except RLS policies, triggers, and `set_config`.
2. **Strict Multi-Tenancy**: Every per-customer table carries `OrgId`, a global EF query filter, and a PostgreSQL RLS policy.
3. **Plain Property Bags**: Entities have no logic or computed properties; all Data Annotations have explicit `ErrorMessage` strings.
4. **Shared Kernel Foundations**: Document headers, lines, tax rows, lifecycle transitions, numbering series, and GST calculations are derived from reusable base classes in `Shared.Kernel`.
5. **No Document Row is Ever Deleted**: Statutory Indian consecutive numbering requires allocating document numbers at creation; draft abandonment is handled via `DocumentStatus.Void` with a mandatory reason.

---

## 2. Base Entity Hierarchy in `Shared.Kernel`

The sales domain entities in `backend/Api/Sales/Sales.Entity/` inherit from a 3-tier base class hierarchy:

```
AuditableEntity (Shared.Kernel.Entities)
  │   - CreatedBy: Guid?
  │   - CreatedAt: DateTimeOffset?
  │   - ModifiedBy: Guid?
  │   - ModifiedAt: DateTimeOffset?
  │   - Version: uint (mapped to Postgres 'xmin' system column concurrency token)
  │
  └── OrgScopedEntity (Shared.Kernel.Tenancy)
        │   - OrgId: Guid (tenant isolation boundary)
        │
        ├── DocumentHeaderBase (Shared.Kernel.Documents)
        ├── DocumentLineBase (Shared.Kernel.Documents)
        └── DocumentLineTaxBase (Shared.Kernel.Documents)
```

### 2.1. `AuditableEntity` (`backend/Shared/Shared.Kernel/Entities/AuditableEntity.cs`)
- Carries the 4 audit fields (`CreatedBy`, `CreatedAt`, `ModifiedBy`, `ModifiedAt`) populated automatically by `AuditSaveChangesInterceptor`.
- Carries `public uint Version { get; set; }` which is mapped in `TenantDbContext` to column `xmin` with `.HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken()`.

### 2.2. `OrgScopedEntity` (`backend/Shared/Shared.Kernel/Tenancy/OrgScopedEntity.cs`)
- Inherits `AuditableEntity`.
- Carries `public Guid OrgId { get; set; }`.

### 2.3. `TenantDbContext` (`backend/Shared/Shared.Kernel/Tenancy/TenantDbContext.cs`)
- Base DbContext for all per-customer microservices (`SalesDbContext`, `AccountingDbContext`, `PurchaseDbContext`, `InventoryDbContext`).
- On `OnModelCreating`:
  - Dynamically builds and registers a global query filter `e => e.OrgId == CurrentOrgId` for all `OrgScopedEntity` types.
  - Dynamically registers an index on `OrgId` for all `OrgScopedEntity` types.
  - Maps `xmin` concurrency token for all `AuditableEntity` types.
- On `SaveChanges` / `SaveChangesAsync`:
  - Enforces `StampOrgId()`: assigns current request `Tenant.Require().OrgId` to newly added entities; throws `InvalidOperationException` if `OrgId` is non-empty and mismatches the request context; marks `OrgId` as unmodified on updates.

---

## 3. Document Base Classes & Fluent Configuration

### 3.1. `DocumentHeaderBase` (`backend/Shared/Shared.Kernel/Documents/DocumentHeaderBase.cs`)
Inherits `OrgScopedEntity`. Provides standard columns for all 9 sales and purchase documents:

| Property | Type | Constraints & Annotations | Description |
|---|---|---|---|
| `TransactionTypeCode` | `string` | `[Required(ErrorMessage = "Transaction type code is required.")]`, `[MaxLength(3)]` | Document type (`INV` or `POS` for invoices) |
| `DocumentNo` | `string` | `[Required(ErrorMessage = "Document number is required.")]`, `[MaxLength(30)]` | Unique document code allocated at creation |
| `DocumentDate` | `DateOnly` | Required | Snapshot date for rates, taxes, and fiscal year |
| `ContactId` | `long` | `[Range(1, long.MaxValue, ErrorMessage = "Choose the contact.")]` | Unenforced Contact ID (resolved cross-service) |
| `ContactGstin` | `string?` | `[MaxLength(15, ErrorMessage = "GSTIN must be 15 characters.")]` | Snapshotted customer GSTIN |
| `BillingAddress` | `string?` | Nullable | Snapshotted billing address |
| `ShippingAddress` | `string?` | Nullable | Snapshotted shipping/dispatch address |
| `PlaceOfSupplyStateId` | `int` | Master data state ID | Snapshotted Place of Supply state ID |
| `IsInterState` | `bool` | Boolean | True if supply is inter-state (IGST), false for intra-state (CGST+SGST) |
| `CurrencyCode` | `string` | `[Required(ErrorMessage = "Currency code is required.")]`, `[MaxLength(3)]` | ISO 3-letter currency code |
| `ExchangeRate` | `decimal` | `decimal(18,8)` | Snapshot exchange rate at `DocumentDate` (default 1.0) |
| `SubTotal` | `decimal` | `decimal(28,2)` | Line gross sum before discounts |
| `DiscountAmount` | `decimal` | `decimal(28,2)` | Total line discount amount |
| `TaxableAmount` | `decimal` | `decimal(28,2)` | Total value on which tax is assessed |
| `CgstAmount` | `decimal` | `decimal(28,2)` | Sum of CGST tax rows |
| `SgstAmount` | `decimal` | `decimal(28,2)` | Sum of SGST tax rows |
| `IgstAmount` | `decimal` | `decimal(28,2)` | Sum of IGST tax rows |
| `CessAmount` | `decimal` | `decimal(28,2)` | Sum of Cess tax rows |
| `RoundOffAmount` | `decimal` | `decimal(28,2)` | Signed rounding difference to nearest whole rupee |
| `TotalAmount` | `decimal` | `decimal(28,2)` | Final payable amount in document currency |
| `TotalAmountBase` | `decimal` | `decimal(28,2)` | `TotalAmount * ExchangeRate` in branch base currency |
| `Status` | `DocumentStatus` | Enum (`Draft=0`, `ReadyToPost=1`, `Posted=2`, `Void=3`) | Document lifecycle state |
| `PostedAt` | `DateTimeOffset?`| Nullable | Timestamp when posted to books |
| `PostedBy` | `Guid?` | Nullable | User ID who executed posting |
| `VoidedAt` | `DateTimeOffset?`| Nullable | Timestamp when voided |
| `VoidedBy` | `Guid?` | Nullable | User ID who voided document |
| `VoidReason` | `string?` | `[MaxLength(300, ErrorMessage = "Void reason cannot exceed 300 characters.")]` | Mandatory reason when `Status == Void` |
| `Notes` | `string?` | Nullable | Customer-facing document notes |
| `TermsAndConditions`| `string?` | Nullable | Terms printed on document |

### 3.2. `DocumentLineBase` (`backend/Shared/Shared.Kernel/Documents/DocumentLineBase.cs`)
Inherits `OrgScopedEntity`. Standard columns for all document line items:

| Property | Type | Constraints & Annotations | Description |
|---|---|---|---|
| `LineNumber` | `int` | `[Range(1, int.MaxValue, ErrorMessage = "Line number starts at one.")]` | 1-based sequential position on document |
| `ItemId` | `long?` | Nullable (no FK, unenforced) | Inventory item ID (null on free-text / service lines) |
| `HsnSacCode` | `string?` | `[MaxLength(8, ErrorMessage = "HSN/SAC code cannot exceed 8 characters.")]` | HSN/SAC code snapshot |
| `Description` | `string?` | `[MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]` | Line description (required if `ItemId` is null) |
| `WarehouseId` | `long?` | Nullable | Dispatch location dimension |
| `Quantity` | `decimal` | `decimal(18,6)` | Entered quantity in `UomId` |
| `UomId` | `long?` | Nullable | Unit of Measure ID |
| `ConversionFactor`| `decimal` | `decimal(18,6)` | Conversion ratio to base stock UOM (default 1.0) |
| `BaseQuantity` | `decimal` | `decimal(18,6)` | `Quantity * ConversionFactor` (what stock moves by) |
| `UnitPrice` | `decimal` | `decimal(28,6)` | Price per entered unit |
| `IsPriceInclusive`| `bool` | Boolean | True if `UnitPrice` is tax-inclusive (MRP) |
| `DiscountPercent` | `decimal?` | `decimal(9,6)?` | Optional discount % (0 to 100) |
| `DiscountAmount` | `decimal` | `decimal(28,2)` | Computed or keyed discount amount |
| `GrossAmount` | `decimal` | `decimal(28,2)` | `Quantity * UnitPrice` before discount |
| `TaxableAmount` | `decimal` | `decimal(28,2)` | Base amount subject to tax |
| `TaxTreatment` | `TaxTreatment` | Enum: `Taxable(0)`, `ZeroRated(1)`, `NilRated(2)`, `Exempt(3)`, `NonGst(4)` | GST category snapshot |
| `TaxMasterId` | `long?` | Nullable | Accounting Tax Master ID |
| `TaxGroupId` | `long?` | Nullable | Accounting Tax Group ID |
| `TaxAmount` | `decimal` | `decimal(28,2)` | Sum of child tax row amounts |
| `LineType` | `DocumentLineType`| Enum: `Stock(0)`, `Expense(1)`, `Capital(2)` | Stock vs Service vs Asset disposal |
| `AccountId` | `long?` | Nullable | Nominal account (required for Expense / free-text) |
| `FixedAssetCategoryId`| `long?` | Nullable | Fixed asset category (required for Capital) |
| `LineTotal` | `decimal` | `decimal(28,2)` | `TaxableAmount + TaxAmount` |
| `ItemBatchId` | `long?` | Nullable | Specific lot / batch ID |
| `LineNotes` | `string?` | `[MaxLength(300, ErrorMessage = "Line notes cannot exceed 300 characters.")]` | Line notes |

### 3.3. `DocumentLineTaxBase` (`backend/Shared/Shared.Kernel/Documents/DocumentLineTaxBase.cs`)
Inherits `OrgScopedEntity`. Grain is `(LineId, TaxComponent)`:

| Property | Type | Constraints | Description |
|---|---|---|---|
| `TaxComponent` | `TaxComponent` | Enum: `Cgst(0)`, `Sgst(1)`, `Igst(2)`, `Cess(3)` | Tax component type |
| `SubAccountId` | `long` | Unenforced ID | Accounting GST sub-account ID |
| `Rate` | `decimal` | `decimal(9,4)` | Tax percentage rate snapshot |
| `TaxableAmount` | `decimal` | `decimal(28,2)` | Taxable base for this specific component |
| `Amount` | `decimal` | `decimal(28,2)` | Tax amount in document currency |
| `AmountBase` | `decimal` | `decimal(28,2)` | Tax amount in base currency (`Amount * ExchangeRate`) |

### 3.4. `DocumentModelConfiguration` (`backend/Shared/Shared.Kernel/Documents/DocumentModelConfiguration.cs`)
Centralized EF Core Fluent mapping defining check constraints, indexes, column precision, and string conversions:
- **Header check constraints**:
  - `chk_{table}_type`: `TransactionTypeCode IN ('INV', 'POS')`
  - `chk_{table}_posted_stamp`: `(Status IN ('Posted', 'Void')) OR PostedAt IS NULL`
  - `chk_{table}_posted_requires_stamp`: `Status <> 'Posted' OR PostedAt IS NOT NULL`
  - `chk_{table}_void_stamp`: `(Status = 'Void') = (VoidedAt IS NOT NULL) AND (VoidedAt IS NOT NULL) = (VoidReason IS NOT NULL)`
  - `chk_{table}_rate_positive`: `ExchangeRate > 0`
  - `chk_{table}_amounts_non_negative`: All monetary amounts $\ge 0$ (except `RoundOffAmount`)
  - `chk_{table}_total`: `TotalAmount = TaxableAmount + CgstAmount + SgstAmount + IgstAmount + CessAmount + RoundOffAmount`
  - `chk_{table}_tax_split`: `(IsInterState AND CgstAmount = 0 AND SgstAmount = 0) OR (NOT IsInterState AND IgstAmount = 0)`
- **Line check constraints**:
  - `chk_{table}_quantity`: `Quantity > 0`
  - `chk_{table}_base_quantity`: `BaseQuantity = round(Quantity * ConversionFactor, 6)`
  - `chk_{table}_discount`: `DiscountAmount >= 0 AND DiscountAmount <= GrossAmount`
  - `chk_{table}_total`: `LineTotal = TaxableAmount + TaxAmount`
  - `chk_{table}_describes`: `ItemId IS NOT NULL OR Description IS NOT NULL`
  - `chk_{table}_free_text`: `ItemId IS NOT NULL OR (AccountId IS NOT NULL AND LineType <> 'Stock')`
  - `chk_{table}_line_type`: `LineType` matches presence of `ItemId`, `AccountId`, `FixedAssetCategoryId`
  - `chk_{table}_untaxed`: Untaxed treatments have `TaxAmount = 0` and no `TaxMasterId`
- **Line tax check constraints**:
  - `chk_{table}_non_negative`: `Rate >= 0 AND TaxableAmount >= 0 AND Amount >= 0 AND AmountBase >= 0`
- **Unique Indexes**:
  - `IX_{table}_Number`: Unique on `(OrgId, DocumentNo)`
  - `IX_{table}_Line`: Unique on `(ParentId, LineNumber)`
  - `IX_{table}_Grain`: Unique on `(ParentLineId, TaxComponent)`

---

## 4. Specific Invoice Entities: `sal.Invoices` and `sal.InvoiceDetails`

In `backend/Api/Sales/Sales.Entity/TableEntities/`, the invoice entities are:

### 4.1. `Invoice` (`backend/Api/Sales/Sales.Entity/TableEntities/Invoice.cs`)
```csharp
public class Invoice : DocumentHeaderBase
{
    public long InvoiceId { get; set; }

    // Source links (Real FKs)
    public long? QuoteId { get; set; }
    public long? SalesOrderId { get; set; }
    public long? DeliveryChallanId { get; set; }

    // Payment terms
    public long? PaymentTermId { get; set; }
    public DateOnly? DueDate { get; set; }

    // POS Sale specifics (Null on INV, required on POS)
    public long? TillId { get; set; }
    public Guid? CashierUserId { get; set; }
    [MaxLength(20, ErrorMessage = "Payment mode cannot exceed 20 characters.")]
    public string? PaymentMode { get; set; }
    public decimal? TenderedAmount { get; set; }
    public decimal? ChangeAmount { get; set; }

    public List<InvoiceDetail> Lines { get; set; } = [];
}
```
**Invoice Header Specific Check Constraints** (in `SalesDbContext.cs`):
- `chk_invoices_pos_fields`: `("TransactionTypeCode" <> 'POS') OR ("TillId" IS NOT NULL AND "PaymentMode" IS NOT NULL)`
- `chk_invoices_due_date`: `("TransactionTypeCode" <> 'INV') OR ("DueDate" IS NOT NULL)`
- `chk_invoices_tender_non_negative`: `("TenderedAmount" IS NULL OR "TenderedAmount" >= 0) AND ("ChangeAmount" IS NULL OR "ChangeAmount" >= 0)`

### 4.2. `InvoiceDetail` (`backend/Api/Sales/Sales.Entity/TableEntities/InvoiceDetail.cs`)
```csharp
public class InvoiceDetail : DocumentLineBase
{
    public long InvoiceDetailId { get; set; }
    public long InvoiceId { get; set; }

    public long? SalesOrderDetailId { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public long? StockMovementId { get; set; }
    public decimal UnitCost { get; set; }

    public List<InvoiceDetailTax> Taxes { get; set; } = [];
}
```
**Invoice Line Specific Check Constraints**:
- `chk_invoicedetails_returned`: `"ReturnedQuantity" >= 0 AND "ReturnedQuantity" <= "Quantity"`

### 4.3. `InvoiceDetailTax` (`backend/Api/Sales/Sales.Entity/TableEntities/InvoiceDetailTax.cs`)
```csharp
public class InvoiceDetailTax : DocumentLineTaxBase
{
    public long InvoiceDetailTaxId { get; set; }
    public long InvoiceDetailId { get; set; }
}
```

---

## 5. SalesDbContext Configuration & Multi-Tenancy

In `backend/Api/Sales/Sales.Repository/SalesDbContext.cs`:
- Inherits `TenantDbContext`.
- Default schema: `sal`.
- Registered DbSets:
  - `DbSet<Quote> Quotes`
  - `DbSet<QuoteDetail> QuoteDetails`
  - `DbSet<QuoteDetailTax> QuoteDetailTaxes`
  - `DbSet<SalesOrder> SalesOrders`
  - `DbSet<SalesOrderDetail> SalesOrderDetails`
  - `DbSet<SalesOrderDetailTax> SalesOrderDetailTaxes`
  - `DbSet<DeliveryChallan> DeliveryChallans`
  - `DbSet<DeliveryChallanDetail> DeliveryChallanDetails`
  - `DbSet<DeliveryChallanDetailTax> DeliveryChallanDetailTaxes`
  - `DbSet<Invoice> Invoices`
  - `DbSet<InvoiceDetail> InvoiceDetails`
  - `DbSet<InvoiceDetailTax> InvoiceDetailTaxes`
  - `DbSet<CreditNote> CreditNotes`
  - `DbSet<CreditNoteDetail> CreditNoteDetails`
  - `DbSet<CreditNoteDetailTax> CreditNoteDetailTaxes`
  - `DbSet<SalesRegister> SalesRegister`
  - `DbSet<NumberingSeries> NumberingSeries` (mapped, `ownsMigration: false`)
- Entity relationships:
  - `Invoice` $\rightarrow$ `InvoiceDetail` (Cascade delete)
  - `InvoiceDetail` $\rightarrow$ `InvoiceDetailTax` (Cascade delete)
  - `Invoice` $\rightarrow$ `Quote` (Restrict delete)
  - `Invoice` $\rightarrow$ `SalesOrder` (Restrict delete)
  - `Invoice` $\rightarrow$ `DeliveryChallan` (Restrict delete)
  - `InvoiceDetail` $\rightarrow$ `SalesOrderDetail` (Restrict delete)
- Migrations & RLS:
  - Migration `20260815125639_AddSalesDocuments` applied `ENABLE ROW LEVEL SECURITY` and policy `invoices_org_isolation` (`USING ("OrgId" = current_setting('app.current_org_id', true)::uuid)`) to `Invoices`, `InvoiceDetails`, `InvoiceDetailTaxes`.
  - Migration `20260818080822_ForceRls` applied `FORCE ROW LEVEL SECURITY` across all tables in `sal`.
  - `base.OnModelCreating(modelBuilder)` is called last, ensuring all tables get the global `OrgId` query filter, `OrgId` index, and `xmin` concurrency token.

---

## 6. Numbering Series Integration

- Series Code: `"INV"` for standard tax invoices; `"POS"` for till counter sales.
- Generator interface: `INumberGenerator` (`backend/Shared/Shared.Kernel/Numbering/INumberGenerator.cs`).
- Allocation method:
  ```csharp
  NumberAllocation alloc = await _numbering.NextAsync("INV", request.DocumentDate, ct);
  invoice.DocumentNo = alloc.Code;
  ```
- **Discipline**:
  - Called at document creation within the save transaction.
  - Number is stored permanently in `DocumentNo`.
  - Document rows are never deleted. If an invoice draft is cancelled or abandoned, `DocumentLifecycle.CanVoid` marks it `DocumentStatus.Void`, preserving the statutory consecutive numbering sequence for GST audits.

---

## 7. GST Tax Calculation & Line Calculations

GST calculation is centralized in pure static methods in `Shared.Kernel.Tax`:

1. **Place of Supply Resolution**:
   ```csharp
   PlaceOfSupplyResult pos = PlaceOfSupply.Resolve(
       branchSettings.StateCode, request.PlaceOfSupplyStateCode, request.ContactGstin);
   // pos.IsInterState determines whether CGST+SGST or IGST applies
   ```
2. **Line Calculation (`GstCalculator.Compute`)**:
   - `GrossAmount = round(Quantity * UnitPrice, 2)`
   - `DiscountAmount = keyedAmount or round(GrossAmount * DiscountPercent / 100, 2)`
   - `Net = DiscountBeforeTax ? GrossAmount - DiscountAmount : GrossAmount`
   - `TaxableAmount = IsPriceInclusive ? round(Net * 100 / (100 + TotalRate), 2) : Net`
   - Component taxes are computed per component (`Cgst`, `Sgst`, `Igst`, `Cess`) using `round(TaxableAmount * ComponentRate / 100, 2)`.
   - `LineTotal = TaxableAmount + TaxAmount`
   - `BaseQuantity = round(Quantity * ConversionFactor, 6)`
3. **Document Totals (`GstCalculator.Totals`)**:
   - Sums rounded line values.
   - `RoundOffAmount = round(TotalAmount, 0) - TotalAmount` (rounds to nearest integer rupee).
   - `TotalAmount = TaxableAmount + Cgst + Sgst + Igst + Cess + RoundOffAmount`
   - `TotalAmountBase = TotalAmount * ExchangeRate`

---

## 8. Posting Engine Integration & Double Entry Rules

When an invoice transitions from `Draft` or `ReadyToPost` to `Posted`:

### 8.1. Inventory Stock Movement
- If `DeliveryChallanId` is present: Goods already shipped via challan. Invoice reads `UnitCost` and `StockMovementId` from challan details and does NOT re-issue stock.
- If `DeliveryChallanId` is null: Calls `IInventoryClient.IssueAsync` with `ReleaseReservation: SalesOrderId.HasValue`. Inventory decrements stock and releases reservation in a single guarded transaction.

### 8.2. General Ledger Posting (`ILedgerClient.PostAsync`)
Posts balanced double-entry legs to `acc.JournalLedger`:
1. **Accounts Receivable / Cash Leg** (`CONTROL`, `LedgerTypeId = 3`):
   - Debit `TotalAmount` against "Accounts Receivable" (or "Cash" for till sale).
   - `SubAccountReferenceType = 1` (Contact), `SubAccountReferenceId = ContactId`, `SubAccountPurpose = 0` (Trade balance).
2. **Sales Revenue Leg** (`ITEM`, `LedgerTypeId = 1`):
   - Credit `SubTotal - DiscountAmount` against "Sales" revenue account.
3. **Tax Payable Legs** (`TAX`, `LedgerTypeId = 2`):
   - Credit per tax rate and component against "Tax Payable" account:
     - CGST: `SubAccountReferenceType = 3`, `SubAccountTaxComponent = 1`
     - SGST: `SubAccountReferenceType = 3`, `SubAccountTaxComponent = 2`
     - IGST: `SubAccountReferenceType = 3`, `SubAccountTaxComponent = 3`
4. **Round-Off Leg** (`ROUNDOFF`, `LedgerTypeId = 6`):
   - If `RoundOffAmount != 0`, posts debit or credit to "Round Off" account.
5. **COGS / Inventory Legs** (`COGS`, `LedgerTypeId = 4` and `CONTROL`, `LedgerTypeId = 3`):
   - Handled via `CostingEngine.Worker`: `Dr Cost of Goods Sold` / `Cr Inventory` (or `Cr Goods Delivered Not Invoiced` if challan).

### 8.3. Sales Register Synchronous Insertion
Inside the posting transaction, writes rows to `sal.SalesRegister` grouped by `(TransactionTypeCode, SourceId, HsnSacCode, GstRate)` for GSTR-1 returns.

---

## 9. Identified Gaps & Refinements for Stage T3.1 Implementation

From our survey of `InvoiceService.cs`, `InvoiceModels.cs`, `InvoicesController.cs`, and `DocumentLineFieldTests.cs`:

1. **DTO Line Field Coverage**:
   - `SaveInvoiceLineRequest` currently only accepts 5 fields (`ItemId`, `Quantity`, `UnitPrice`, `DiscountPercent`, `TaxGroupIds`).
   - Must be expanded to match `SaveSalesOrderLineRequest`: support `HsnSacCode`, `Description`, `WarehouseId`, `UomId`, `ConversionFactor`, `IsPriceInclusive`, `TaxTreatment`, `TaxGroupId`, `LineType`, `AccountId`, `FixedAssetCategoryId`, `ItemBatchId`, `LineNotes`.
2. **`BaseQuantity` and `LineNumber` Assignment**:
   - In `SaveAsync`, `line.LineNumber = i + 1` and `line.BaseQuantity = computed.BaseQuantity` must be assigned to avoid constraint failures (`chk_invoicedetails_base_quantity` and `IX_InvoiceDetails_Line`).
3. **Controller Permissions and Response Standard**:
   - `InvoicesController` should have `[Authorize]`, `[RequireModulePermission("sales")]`, `[PermissionAction("approve")]`, `[PermissionAction("void")]`.
   - Action responses should use `InvoiceResult` with `InvoiceOutcome` enum (`Ok`, `NotFound`, `LifecycleRefused`, `LineInvalid`, `PlaceOfSupplyRefused`, `RatesUnavailable`, `InsufficientStock`, `CreditLimitExceeded`).
4. **Void Reason Mandatory Requirement**:
   - Voiding requires `VoidInvoiceRequest` with `Reason` string; must check `DocumentLifecycle.CanVoid(status, hasDownstream, reason)`.
