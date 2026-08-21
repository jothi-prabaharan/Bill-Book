# Handoff Report: Stage T3.1 — Invoices Backend Domain Models Survey

## 1. Observation
1. **Sales Entity Base Hierarchy**:
   - `AuditableEntity` (`backend/Shared/Shared.Kernel/Entities/AuditableEntity.cs:11`) provides audit properties `CreatedBy`, `CreatedAt`, `ModifiedBy`, `ModifiedAt`, and `Version` (PostgreSQL `xmin`).
   - `OrgScopedEntity` (`backend/Shared/Shared.Kernel/Tenancy/OrgScopedEntity.cs:11`) inherits `AuditableEntity` and adds `public Guid OrgId { get; set; }`.
   - `TenantDbContext` (`backend/Shared/Shared.Kernel/Tenancy/TenantDbContext.cs:18`) automatically attaches the global query filter `e => e.OrgId == CurrentOrgId`, adds an index on `OrgId`, and binds `xmin` concurrency tokens on `OnModelCreating`.
   - `DocumentHeaderBase` (`backend/Shared/Shared.Kernel/Documents/DocumentHeaderBase.cs:25`), `DocumentLineBase` (`backend/Shared/Shared.Kernel/Documents/DocumentLineBase.cs:17`), and `DocumentLineTaxBase` (`backend/Shared/Shared.Kernel/Documents/DocumentLineTaxBase.cs:23`) define shared trading document schema and check constraints.

2. **Invoice Entities & Schema**:
   - `Invoice` (`backend/Api/Sales/Sales.Entity/TableEntities/Invoice.cs:18`) inherits `DocumentHeaderBase` and declares `InvoiceId`, source links `QuoteId`, `SalesOrderId`, `DeliveryChallanId`, payment terms `PaymentTermId`, `DueDate`, and POS fields `TillId`, `CashierUserId`, `PaymentMode`, `TenderedAmount`, `ChangeAmount`.
   - `InvoiceDetail` (`backend/Api/Sales/Sales.Entity/TableEntities/InvoiceDetail.cs:6`) inherits `DocumentLineBase` and declares `InvoiceDetailId`, `InvoiceId`, `SalesOrderDetailId`, `ReturnedQuantity`, `StockMovementId`, `UnitCost`, and `Taxes`.
   - `InvoiceDetailTax` (`backend/Api/Sales/Sales.Entity/TableEntities/InvoiceDetailTax.cs:9`) inherits `DocumentLineTaxBase` and declares `InvoiceDetailTaxId`, `InvoiceDetailId`.

3. **SalesDbContext & Migrations**:
   - `SalesDbContext` (`backend/Api/Sales/Sales.Repository/SalesDbContext.cs:26`) registers `DbSet<Invoice> Invoices`, `DbSet<InvoiceDetail> InvoiceDetails`, and `DbSet<InvoiceDetailTax> InvoiceDetailTaxes` in schema `sal`.
   - `DocumentModelConfiguration` (`backend/Shared/Shared.Kernel/Documents/DocumentModelConfiguration.cs:27`) configures standard header, line, and tax check constraints and indexes (`IX_Invoices_Number`, `IX_InvoiceDetails_Line`, `IX_InvoiceDetailTaxes_Grain`).
   - Migrations `20260815125639_AddSalesDocuments.cs` and `20260818080822_ForceRls.cs` enable and force RLS policies (`USING ("OrgId" = current_setting('app.current_org_id', true)::uuid)`).

4. **Integration Services**:
   - Numbering: `INumberGenerator.NextAsync("INV", request.DocumentDate, ct)` (`backend/Shared/Shared.Kernel/Numbering/INumberGenerator.cs:21`).
   - GST Math: `GstCalculator.Compute` and `GstCalculator.Totals` (`backend/Shared/Shared.Kernel/Tax/GstCalculator.cs:76, 119`).
   - Ledger: `ILedgerClient.PostAsync` (`backend/Api/Sales/Sales.Api/Services/LedgerClient.cs:59`).
   - Inventory: `IInventoryClient.IssueAsync` (`backend/Api/Sales/Sales.Api/Services/InventoryClient.cs:38`) with `ReleaseReservation: SalesOrderId.HasValue`.

5. **Existing Implementation Gaps Documented in Tests & Code**:
   - `DocumentLineFieldTests.cs:31` documents that `InvoiceDetail` saves must assign `BaseQuantity = Quantity * ConversionFactor` and `LineNumber = i + 1`.
   - `SaveInvoiceLineRequest` in `InvoiceModels.cs:103` currently omits `HsnSacCode`, `Description`, `WarehouseId`, `UomId`, `ConversionFactor`, `IsPriceInclusive`, `TaxTreatment`, `LineType`, `AccountId`.
   - `InvoicesController.cs:9` lacks `[Authorize]`, `[RequireModulePermission("sales")]`, `[PermissionAction("approve")]`, and `[PermissionAction("void")]`.

## 2. Logic Chain
1. Multi-tenancy isolation in Bill-Book relies on 3 simultaneous enforcement levels: `OrgId` column + EF Core global query filter in `TenantDbContext` + PostgreSQL RLS policy.
2. The database check constraints configured via `DocumentModelConfiguration` strictly enforce integrity: `Quantity > 0`, `BaseQuantity = round(Quantity * ConversionFactor, 6)`, `LineTotal = TaxableAmount + TaxAmount`, unique `(InvoiceId, LineNumber)` and `(InvoiceDetailId, TaxComponent)`.
3. Invoices must allocate `DocumentNo` at draft creation via `INumberGenerator.NextAsync("INV", ...)`. Document rows are never deleted; cancellation transitions the status to `DocumentStatus.Void` with a mandatory reason string.
4. When posting an invoice:
   - If raised directly or from a sales order, `IInventoryClient.IssueAsync` is called with `ReleaseReservation: SalesOrderId.HasValue`.
   - If raised against a delivery challan (`DeliveryChallanId.HasValue`), stock was already moved by the challan, so the invoice reuses challan movement details and skips new stock issues.
   - General Ledger entries are posted via `ILedgerClient.PostAsync` with debit `CONTROL` (Accounts Receivable / Cash), credit `ITEM` (Sales), credit `TAX` (Output GST components: CGST, SGST, IGST), and `ROUNDOFF`.
   - Synchronous entries are written to `sal.SalesRegister` for statutory GSTR-1 reporting.

## 3. Caveats
- Frontend components (`libs/sales/sales-ui`) and UI interaction flows were not modified (read-only exploration).
- POS terminal specific logic (e.g. Till management, ESC/POS printing) is slated for Phase 3; however, the invoice backend domain entities (`Invoice`, `InvoiceDetail`) already accommodate POS records via `TransactionTypeCode = 'POS'`.
- Background COGS posting is performed asynchronously by `CostingEngine.Worker`.

## 4. Conclusion
The backend domain models (`Invoice`, `InvoiceDetail`, `InvoiceDetailTax`), database schema mappings, check constraints, RLS policies, numbering series, tax calculations, and ledger posting contracts are well-defined in `Shared.Kernel` and `Sales.Repository`. To fully complete Stage T3.1:
1. Align `SaveInvoiceRequest` and `SaveInvoiceLineRequest` in `InvoiceModels.cs` with the full 18-field surface of `DocumentLineBase` (similar to `SalesOrderModels.cs`).
2. Update `InvoiceService.cs` to set `LineNumber`, `BaseQuantity`, and utilize `GstCalculator.Compute` / `GstCalculator.Totals`.
3. Secure `InvoicesController.cs` with `[Authorize]`, `[RequireModulePermission("sales")]`, `[PermissionAction]`, and structured `InvoiceResult` outcome mappings.
4. Implement the ledger posting and inventory release-then-issue transaction flow.

## 5. Verification Method
- Code Inspection:
  - `backend/Api/Sales/Sales.Entity/TableEntities/Invoice.cs`
  - `backend/Api/Sales/Sales.Entity/TableEntities/InvoiceDetail.cs`
  - `backend/Api/Sales/Sales.Entity/TableEntities/InvoiceDetailTax.cs`
  - `backend/Api/Sales/Sales.Repository/SalesDbContext.cs`
- Build & Test verification commands:
  ```powershell
  cd backend
  dotnet build --no-incremental
  dotnet test backend/Tests/Sales.Api.Tests/
  dotnet test backend/Tests/Shared.Kernel.Tests/
  ```
- Validation condition: `SalesQueryFilterTests` and `DocumentLineFieldTests` pass against PostgreSQL, confirming query filter presence, concurrency mapping, and RLS enforcement.
