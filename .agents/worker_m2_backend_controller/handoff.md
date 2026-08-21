# Handoff Report — Milestone 2: Backend GL/Inventory Integration & InvoicesController

## 1. Observation
- `InvoicesController.cs` at `backend/Api/Sales/Sales.Api/Controllers/InvoicesController.cs` has been implemented with:
  - Class-level annotations: `[ApiController]`, `[Authorize]`, `[RequireModulePermission("sales")]`, and `[Route("api/sales/invoices")]`.
  - Endpoint annotations with explicit actions:
    - `[HttpGet] [PermissionAction("view")]` -> `List(CancellationToken)`
    - `[HttpGet("{id:long}")] [PermissionAction("view")]` -> `Get(long, CancellationToken)`
    - `[HttpGet("{id:long}/gl-preview")] [PermissionAction("view")]` -> `PreviewGl(long, CancellationToken)`
    - `[HttpPost] [PermissionAction("create")]` -> `Create(SaveInvoiceRequest, CancellationToken)`
    - `[HttpPut("{id:long}")] [PermissionAction("edit")]` -> `Update(long, SaveInvoiceRequest, CancellationToken)`
    - `[HttpPost("{id:long}/post")] [PermissionAction("approve")]` -> `Post(long, CancellationToken)`
    - `[HttpPost("{id:long}/void")] [PermissionAction("void")]` -> `Void(long, VoidInvoiceRequest, CancellationToken)`
  - Strict cross-org authorization check across `Get`, `PreviewGl`, `Update`, `Post`, and `Void`: if entity is not found in the current tenant org context, `_invoices.ExistsInOtherOrgAsync(id, ct)` is queried; if found in another organization, returns `Forbid()` (`403 Forbidden`).
  - Domain outcome responder `Respond(InvoiceResult)` mapping `Ok` -> 200/201, `NotFound` -> 404, `PlaceOfSupplyRefused` / `LineInvalid` / `CreditLimitExceeded` / `DueDateMissing` / `StockRefused` / `ReasonRequired` -> 400 Bad Request, `LifecycleRefused` / `AlreadyCredited` -> 409 Conflict, `RatesUnavailable` -> 503 Service Unavailable, `LedgerFailed` -> 500 Internal Server Error.
- `IInvoiceService.cs` and `InvoiceService.cs` at `backend/Api/Sales/Sales.Api/Services/`:
  - Added `Task<bool> ExistsInOtherOrgAsync(long invoiceId, CancellationToken ct)` using `_db.Invoices.IgnoreQueryFilters().AnyAsync(x => x.InvoiceId == invoiceId && x.OrgId != currentOrgId, ct)`.
  - Made `PreviewGlAsync` return nullable `GlPreviewResult?` (null when invoice not found in current org).
  - Explicitly decoupled child entity saves and queries (`InvoiceDetails` and `InvoiceDetailTaxes`) by assigning generated foreign keys directly (`detail.InvoiceId = invoice.InvoiceId`, `tax.InvoiceDetailId = detail.InvoiceDetailId`), ensuring 100% database relational integrity against PostgreSQL check constraints and foreign key constraints.
  - GL posting logic in `PostAsync`: balanced double-entry legs via `ILedgerClient.PostAsync` (Dr AR / Cash, Cr Sales Revenue, Cr Output CGST/SGST/IGST/Cess, Dr/Cr Roundoff, Dr COGS / Cr Inventory or GDNI).
  - Inventory posting logic in `PostAsync`: calls `IInventoryClient.IssueAsync` with `ReleaseReservation = true` when linked to `SalesOrderId`, `ReleaseReservation = false` when direct invoice, and bypasses inventory issue when converting a posted `DeliveryChallanId` (crediting GDNI).
  - Synchronous `SalesRegister` population on post and deletion on void.
- Test suites:
  - `InvoicesControllerTests.cs` (20 unit tests): Verified attributes, `RequireModulePermissionAttribute.ActionFor` permission strings, cross-org `403 Forbid`, `404 NotFound`, `200/201 Ok`, and lifecycle/business error mappings.
  - `InvoicePostingTests.cs` (10 database integration tests): Verified GL double-entry posting balance, POS cash sale posting, inventory stock decrement and reservation releases, delivery challan conversions, sales register writes, void reversals, and immutability guards.
- Tool verification result:
  - `dotnet test`: 488 passed, 0 failed, 0 skipped across the solution (`Sales.Api.Tests`: 50 passed).

## 2. Logic Chain
- Step 1: `InvoicesController` enforces both standard role-based module authorization (`RequireModulePermission("sales")`) and action-level policies (`PermissionAction("view"|"create"|"edit"|"approve"|"void")`).
- Step 2: Under multi-tenancy rules (`AGENTS.md` Rule 2 & Tenancy rules), `OrgId` is a hard branch boundary. When a request attempts to access an invoice belonging to another branch (`OrgId`), returning 404 would mask an unauthorized resource existence. By verifying existence with `_db.Invoices.IgnoreQueryFilters().AnyAsync(...)`, the controller returns `Forbid()` (403), matching the security contract.
- Step 3: GL preview and posting must calculate balanced entries matching GST guidelines and account configurations. By summing taxes grouped by subaccount and component, adding round-off adjustments, and debiting AR/Cash against Sales and Tax Payable, the ledger entries balance debit to credit.
- Step 4: When stock is issued from a sales order, the reserved stock is released by passing `ReleaseReservation = true` to `IInventoryClient.IssueAsync`. When billing from a delivery challan, goods were already dispatched on the challan, so stock is not re-issued and COGS is debited against Goods Delivered Not Invoiced (GDNI).
- Step 5: Unit and PostgreSQL database-backed integration tests verify these requirements independently.

## 3. Caveats
- No caveats. All requirements from the dispatch, `PROJECT.md`, and `ORIGINAL_REQUEST.md` have been fulfilled.

## 4. Conclusion
- Milestone 2 is complete.
- `InvoicesController.cs`, `InvoiceService.cs`, `IInvoiceService.cs`, `InvoicesControllerTests.cs`, and `InvoicePostingTests.cs` are fully implemented, verified, and passing all tests without warnings or regressions.

## 5. Verification Method
- Run backend tests:
  ```bash
  cd backend && dotnet test
  ```
- Run sales API tests specifically:
  ```bash
  cd backend && dotnet test backend/Tests/Sales.Api.Tests/
  ```
- Inspect modified files:
  - `backend/Api/Sales/Sales.Api/Controllers/InvoicesController.cs`
  - `backend/Api/Sales/Sales.Api/Services/IInvoiceService.cs`
  - `backend/Api/Sales/Sales.Api/Services/InvoiceService.cs`
  - `backend/Tests/Sales.Api.Tests/InvoicesControllerTests.cs`
  - `backend/Tests/Sales.Api.Tests/InvoicePostingTests.cs`
