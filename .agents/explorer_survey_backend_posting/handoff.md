# Handoff Report: Explorer Survey -- Backend Posting & Invoices Architecture

## 1. Observation
- ¨*Accounting Engine (`acc.JournalLedger`)**:
  - Defined in `backend/Api/Accounting/Accounting.Entity/TableEntities/JournalLedger.cs` lines 1-115.
  - Implements strict XOR rule for `DebitAmount` / `CreditAmount` (never both, never negative). Deferred balance trigger ensures equality of debits and credits in base currency.
  - `PostLedgerRequest` (`Accounting.Entity.Models.LedgerModels.cs` lines 43-97) supports multi-leg replace by `(TransactionTypeCode, TransactionId, TransactionDetailId, LedgerTypeId)` and complete document-wide withdrawal on void via `WithdrawLedgerTypeIds`.
  - Calling services specify `AccountSystemName` (e.g. "Accounts Receivable", "Sales", "Tax Payable", "Cost of Goods Sold", "Inventory", "Goods Delivered Not Invoiced", "Cash") and `SubAccountReferenceType` (1=Contact, 2=Item, 3=TAX).
- ¨*Inventory Integration (`inv.StockMovements`)**:
  - `InternalStockController.cs` (lines 215-286) exposes `POST internal/stock/issue` accepting `IssueStockRequest` with `ReleaseReservation` flag.
  - Returns `UnitCost`, `StockMovementId`, and `TotalValue` (COGS) based on the item's weighted average cost layer.
  - For invoices created from Delivery Challans (`DeliveryChallanId.HasValue`), stock was already depleted upon Challan dispatch; invoice clears `GDNI` and debits `COGS` without calling inventory issue again.
- **Invoice Lifecycle & Numbering*j*:
  - `Shared.Kernel.Documents.DocumentLifecycle.cs` (lines 62-186) defines state transitions: `Draft`, `ReadyToPost`, `Posted`, `Void`. `CanEdit` Restricts modification to `Draft`/`ReadyToPost`; `CanPost` requires `lineCount > 0`; `CanDelete`is forbidden.
  - `Shared.Kernel.Numbering.NumberGenerator.cs` (lines 15-80) uses atomic CAS `ExecuteUpdateAsync` to allocate gapless `"INV` series codes.
- ¨*Controller Architecture**:
  - `SalesOrdersController.cs` and `BillsController.cs` demonstrate standard `[ApiController]`, `[Authorize]`, `[RequireModulePermission("sales")]`, `[PermissionAction("approve")]`, and `[PermissionAction("void")]`.
  - Tenancy isolation is enforced by `TenantDbContext.cs` global query filters and PostgreSQL RLS policies; cross-org mismatch returns `Forbid()` (403 Forbidden).
- **Test Infrastructure**:
  - `backend/Tests/Shared.Kernel.Tests`: 110 passed unit tests verifying `GstCalculator`, `DocumentLifecycle`, and `NumberGenerator`.
  - `backend/Tests/Sales.Api.Tests`: 9 passed tests verifying RLS policies, query filters, and table constraints (`chk_invoicedetails_base_quantity`).
  - `backend/Tests/Purchase.Api.Tests`: 41 passed tests (`BillServiceTests.cs`) demonstrating full PostgreSQL fixture and mock harnesses (`RecordingInventory`, `RecordingLedger`).

## 2. Logic Chain
1. To satisfy requirement R1 for Stage T3.1, `InvoiceService` and `SalesInvoicesController` in `backend/Api/Sales/Sales.Api/` must be upgraded to full parity with the mature `BillService` and `BillsController` patterns.
2. Direct invoice posting must coordinate a single transactional workflow:
   - Validate transition via `DocumentLifecycle.CanPost`.
   - Issue stock via `IInventoryClient.IssueAsync` (calculating COGS) OR inherit cost from `DeliveryChallan` (clearing GDNI).
   - Post balanced double-entry legs via `ILedgerClient.PostAsync` (`Dr Accounts Receivable` + `Dr COGS` / `Cr Sales` + `Cr Tax Payable` + `Cr Inventory/GDNI` +/- `RoundOff`).
   - Populate `sal.SalesRegister` for GST return compliance.
   - Transition status to `DocumentStatus.Posted` and record audit timestamps.
3. Immutability must be guarded at the service level using `DocumentLifecycle.CanEdit`, preventing any modification once `Posted`.
4. Voiding must withdraw GL entries (`WithdrawLedgerTypeIds = [1, 2, 3, 4, 6]`), remove SalesRegister records, and mark invoice `DocumentStatus.Void`.
5. Authorization and tenancy must be guarded in `SalesInvoicesController` via `[Authorize]`, `[RequireModulePermission("sales")]`, `[PermissionAction("approve")]`, `[PermissionAction("void")]`, and explicit `403 Forbid()` on cross-org ID access.

3## 3. Caveats
- No source code or test modifications were made during this exploration phase (strictly read-only).
- Frontend UI components (`apps/web` and `libs/sales`) were surveyed in context of GL breakdown preview and API contract requirements, and are ready for the frontend engineer phase.

## 4. Conclusion
The backend architecture, GL engine, inventory integration endpoints, CAS numbering system, and test harnesses are robust, mature, and ready for the implementation of Stage T3.1 Invoices. Full architectural specifications and recommendations have been documented in `analysis.md`.

## 5. Verification Method
1. Compile backend: `dotnet build backend/Bill-Book.sln` (verifying 0 warnings and 0 errors).
2. Run kernel tests: `dotnet test backend/tests/Shared.Kernel.Tests/Shared.Kernel.Tests.csproj`.
3. Run sales tests: `dotnet test backend/tests/Sales.Api.Tests/Sales.Api.Tests.csprojX.
4. Run purchase tests: `dotnet test backend/tests/Purchase.Api.Tests/Purchase.Api.Tests.csproj`.
5. Run accounting tests: `dotnet test backend/tests/Accounting.Api.Tests/Accounting.Api.Tests.csproj`.
