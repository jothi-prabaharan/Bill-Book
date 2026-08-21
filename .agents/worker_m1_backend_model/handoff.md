# Handoff Report: Milestone 1 — Backend Domain Models, DbContext & Service Foundation

## 1. Observation
1. **Domain Entities**:
   - `Invoice` (`backend/Api/Sales/Sales.Entity/TableEntities/Invoice.cs`): Plain property bag inheriting `DocumentHeaderBase` (which inherits `OrgScopedEntity` and `AuditableEntity`). Declares `InvoiceId`, source links `QuoteId`, `SalesOrderId`, `DeliveryChallanId`, payment term `PaymentTermId`, `DueDate`, and POS properties `TillId`, `CashierUserId`, `PaymentMode`, `TenderedAmount`, `ChangeAmount` with explicit `ErrorMessage` data annotations.
   - `InvoiceDetail` (`backend/Api/Sales/Sales.Entity/TableEntities/InvoiceDetail.cs`): Plain property bag inheriting `DocumentLineBase`. Declares `InvoiceDetailId`, `InvoiceId`, `SalesOrderDetailId`, `ReturnedQuantity`, `StockMovementId`, `UnitCost`, and `Taxes` list.
   - `InvoiceDetailTax` (`backend/Api/Sales/Sales.Entity/TableEntities/InvoiceDetailTax.cs`): Plain property bag inheriting `DocumentLineTaxBase`. Declares `InvoiceDetailTaxId`, `InvoiceDetailId`.
2. **DTO Models**:
   - `InvoiceModels` (`backend/Api/Sales/Sales.Entity/Models/InvoiceModels.cs`): Fully completed with `SaveInvoiceRequest`, `SaveInvoiceLineRequest` (including `HsnSacCode`, `WarehouseId`, `UomId`, `ConversionFactor`, `IsPriceInclusive`, `TaxTreatment`, `LineType`, `AccountId`, `FixedAssetCategoryId`, `ItemBatchId`, `LineNotes`, `SalesOrderDetailId`), `InvoiceOutcome`, `InvoiceResult`, `InvoiceListItem`, `InvoiceSummary`, `InvoiceView`, `InvoiceLineView`, `InvoiceLineTaxView`, `InvoiceTaxView`, `VoidInvoiceRequest`, `GlPreviewResult`, `GlEntryLegView`.
3. **DbContext & Configuration**:
   - `SalesDbContext` (`backend/Api/Sales/Sales.Repository/SalesDbContext.cs`): Registers `Invoices`, `InvoiceDetails`, and `InvoiceDetailTaxes` with check constraints (`chk_invoices_pos_fields`, `chk_invoices_due_date`, `chk_invoices_tender_non_negative`, `chk_invoicedetails_returned`) and standard header/line/tax configurations (`DocumentModelConfiguration`). Global query filter on `OrgId` and concurrency token mapping `xmin` verified.
4. **Service Contracts & Implementations**:
   - `IInvoiceService` (`backend/Api/Sales/Sales.Api/Services/IInvoiceService.cs`): Declares interface for `CreateAsync`, `UpdateAsync`, `SaveAsync`, `GetAsync`, `ListAsync`, `PostAsync`, `VoidAsync`, `PreviewGlAsync`.
   - `InvoiceService` (`backend/Api/Sales/Sales.Api/Services/InvoiceService.cs`): Implements draft creation/updates with `GstCalculator.Compute`, `GstCalculator.Totals`, `BaseQuantity = round(Quantity * ConversionFactor, 6)`, `LineNumber` assignment, atomic CAS document numbering via `INumberGenerator.NextAsync("INV", ...)` / `INumberGenerator.NextAsync("POS", ...)`, GL preview, stock issue with reservation release, balanced double-entry GL ledger posting via `ILedgerClient`, synchronous GSTR-1 `SalesRegister` writes, and voiding with ledger withdrawal.
5. **Build & Test Verification**:
   - `dotnet build` executes cleanly with 0 warnings and 0 errors.
   - `dotnet test backend/Tests/Sales.Api.Tests/` passes with 9 passed, 0 failed.
   - Entire test suite across all projects passes with 447 passed, 0 failed.

## 2. Logic Chain
1. Multi-tenancy in Bill-Book requires every sales document entity to inherit `OrgScopedEntity`, which applies the global query filter in `TenantDbContext` and matches the PostgreSQL RLS policy.
2. Check constraints on line items (`chk_invoicedetails_base_quantity`, `IX_InvoiceDetails_Line`) mandate that `BaseQuantity` exactly equals `round(Quantity * ConversionFactor, 6)` and `LineNumber` is 1-based and strictly sequential.
3. Aligning `SaveInvoiceLineRequest` to the 18-field surface of `DocumentLineBase` ensures that stock items, service lines (with `AccountId`), and capital lines (with `FixedAssetCategoryId`) are accurately handled across create, edit, and tax calculation flows.
4. The service implementation follows the transactional lifecycle: Draft saves do not affect stock or general ledger; Posting executes stock issue (releasing reservations when tied to a sales order) and creates balanced double-entry accounting legs; Voiding verifies downstream credit notes and withdraws ledger entries.

## 3. Caveats
- No new NuGet packages were added; all dependencies rely on existing pinned packages in `Directory.Packages.props`.
- Sales Order conversion and controller action authorizations are part of Milestone 2 / Milestone 3.

## 4. Conclusion
Milestone 1 is complete. Domain entities, DTO models, `SalesDbContext` configuration, `IInvoiceService` interface, and `InvoiceService` are fully implemented with genuine business logic and verified passing all builds and test suites.

## 5. Verification Method
- Build Verification:
  ```powershell
  cd backend
  dotnet build
  ```
- Test Verification:
  ```powershell
  cd backend
  dotnet test Tests/Sales.Api.Tests/
  dotnet test
  ```
- All test runs confirm 0 failures and 100% compliance with query filters, model constraints, and tax math.
