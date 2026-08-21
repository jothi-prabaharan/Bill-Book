# Comprehensive Survey & Analysis: Backend Posting Service, Accounting/Inventory Integration, Controllers, and Test Infrastructure for Stage T3.1 -- Invoices

## Executive Summary
This report delivers an in-depth architectural survey of the backend services, integration points, controllers, and test infrastructure required for Stage T3.1 - Invoices (INV). It details the General Ledger (GL) double-entry posting engine, inventory stock depletion and movement mechanisms, invoice posting and lifecycle workflows (including CAS numbering, immutability, and voiding reversals), controller and authorization patterns, and test harnesses.

---

## 1. Accounting Engine & General Ledger (GL) Integration

### 1.1 `acc.JournalLedger` Table Structure & Semantics
- **Single Posting Target**: `JournalLedger` (`Accounting.Entity.TableEntities.JournalLedger`) is the single posting destination across all services (Sales, Purchase, Banking, Inventory). Reporting services read this table directly.
- *(Strict Double-Entry Discipline**:
  - *Grain**: Exactly one row per double-entry leg.
  - **Strict XOR Rule**: Each row carries either a positive `DebitAmount` or positive `CreditAmount` (and base currency equivalents `DebitAmountBase`, `CreditAmountBase`). Amounts are never negative and never both positive on one row. Reversals are posted as the opposite side (e.g. credit to reduce AR), not as negative amounts.
  - **Base Currency Balancing**: A deferred constraint trigger checks that the sum of `DebitAmountBase`quals the sum of `CreditAmountBase` for each `(TransactionTypeCode, TransactionId)` group upon transaction commit.
- ¨*Key Fields**:
  - `LedgerId` (int64, PK)
  - `OrgId` (Guid, inherited from `OrgScopedEntity` with query filter and RLS)
  - `LedgerDate` (DateOnly, used for reporting periods and aging)
  - `AccountId` (int64, GL account)
  - `SubAccountId` (int64?, sub-dimension underneath control accounts)
  - `TransactionTypeCode` (string(3), e.g. "INV", "POS", "CRN", "BIL")
  - `TransactionId` (int64, source header ID, e.g. `InvoiceId`)J  - `TransactionDetailId` (int64, line ID or 0 for document-level legs)
  - `LedgerTypeId` (int: 1=ITEM, 2=TAX, 3=CONTROL, 4=COGS, 5=FX, 6=ROUNDOFF)
  - `LedgerSourceId` (int: from `mst.LedgerSources`, 1=Document Posting, 3=Transaction Posting)
  - `ContactId` (int64?, customer or vendor reference)
  - `CurrencyCode`, `ExchangeRate`, `TaxExchangeRate`

3## 1.2 Chart of Accounts & Sub-Account Resolution
- **Inter-Service Decoupling via System Names**: Calling services do not resolve `AccountId` numbers. They send `AccountSystemName` ("Accounts Receivable", "Sales", "Tax Payable", "Cost of Goods Sold", "Inventory", "Goods Delivered Not Invoiced", "Cash"). `LedgerPostingService` in Accounting resolves the organization-specific `AccountId`.
- ¨*Sub-Account Keys for Control Accounts**:
  - **Contact Sub-Accounts** (`SubAccountReferenceType = 1`): SubAccount under `Accounts Receivable`. `SubAccountPurpose = 0` (Primary/Trade Balance), `1` (Prepayment Advance), `2` (Overpayment Advance).
  - **Tax Sub-Accounts** (`SubAccountReferenceType = 3`): SubAccount under `Tax Payable`. Mapped by tax rate group and `SubAccountTaxComponent` (1=CGST, 2=SGST, 3=IGST).
  - **Item Sub-Accounts** (`SubAccountReferenceType = 2`): For item-level dimensions.

### 1.3 Posting Protocol & Replacement vs Withdrawal
- **Internal API Endpoint**: `POST /internal/ledger/postings` handled by `InternalLedgerController` (guarded with `[InternalOnly]`, `CustomerId` and `OrgId` provided in request body).
- **Selective Replacement**: Posting keys by `(TransactionTypeCode, TransactionId, TransactionDetailId, LedgerTypeId)`. Re-posting updates existing matching legs without clobbering other writers' rows (e.g. Sales revenue/tax legs vs Inventory costing COGS legs).
- **Withdrawal on Void**: When voiding, send empty `Legs = []` in `PostLedgerRequest` and populate `WithdrawLedgerTypeIds = [1, 2, 3, 4, 6]`. This clears all document legs across the entire document in a single call.

---

## 2. Inventory Integration & Stock Depletion

### 2.1 Depletion Workflows
1. **Direct Sales Invoice (No Delivery Challan)**:
   - Invoice calls `IInventoryClient.IssueAsync(IssueStockRequest)` targeting `POST internal/stock/issue`.
   - `InternalStockController.Issue` invokes `StockService.RecordAsync(MovementType.Issue)`.
   - Inventory computes unit cost via weighted average cost layer, creates stock movement records, and returns `StockMovementId`, `UnitCost`, and `TotalValue` (COGS).
   - Sales invoice creates ledger legs: `Dr Cost of Goods Sold` (type 4 COGS) and `Cr Inventory` (type 3 CONTROL).
2. **Invoice from Delivery Challan (`DeliveryChallanId.HasValue`)**:
   - Stock was already depleted when Delivery Challan was posted (`Dr Goods Delivered Not Invoiced / Cr Inventory`).
   - Invoice moves no stock; inherits `UnitCost` and `StockMovementId` from Delivery Challan lines.
   - Invoice posts `Dr Cost of Goods Sold` (type 4 COGS) and `Dr Goods Delivered Not Invoiced` (type 3 CONTROL), clearing the GDNI account.
3. **Invoice from Sales Order (`SalesOrderId.HasValue`)**:
   - Sets `ReleaseReservation = true` on `IssueStockLine` so Inventory releases the reserved stock upon issue.

### 2.2 Reversals & Returns
- Voiding an unposted draft invoice has zero stock impact.
- Voiding a posted invoice withdraws GL entries. Physical return of goods against posted invoices is performed via Credit Notes (`CRN`) with `SalesReturn` reason code, calling `IInventoryClient.ReceiveAsync(...)` to return stock to the original cost layer via `ReturnsStockMovementId`.

---

## 3. Invoice Posting Flow, Immutability & Numbering

### 3.1 Document Lifecycle (`Shared.Kernel.Documents.DocumentLifecycle`)
- **States**: `Draft` (0) -> `ReadyToPost` (1) -> `Posted` (2) -> `Void` (3).
- ¨*Immutability Enforcement**:
  - `CanEdit(status)` permits edits only for `Draft` and `ReadyToPost`. Rejects edits on `Posted` and `Void`.
  - `CanPost(status, lineCount)` permits posting from `Draft` or `ReadyToPost` when `lineCount > 0`.
  - `CanVoid(status, hasDownstream, reason)` requires a non-empty reason and checks that no downstream documents (e.g. payments or credit notes) exist.
  - `CanDelete()` always rejects deletion: invoice rows and statutory numbers are never deleted.

### 3.2 CAS (Compare-And-Swap) Document Number Allocation
- Handled by `NumberGenerator` (`Shared.Kernel.Numbering.INumberGenerator`).
- Uses atomic `ExecuteUpdateAsync` with `s.NextNumber == expected` CAS loop.
- Series code "INV" for standard invoices, "POS" for retail POS sales.
- Allocated at invoice creation time.

### 3.3 Sales Register (`sal.SalesRegister`)
- Upon posting, invoice lines are written to `sal.SalesRegister` with full GST tax breakdown (HSN/SAC, Rate, TaxableAmount, CGST/SGST/IGST, SupplyType B2B/B2CS, PlaceOfSupplyStateId, ContactGstin).
- Protected by `OrgScopedEntity` query filters and RLS.
- Cleared/reversed upon voiding.

---

## 4. Controller & Authorization Patterns

### 4.1 Gold-Standard Controller Architecture
- Reference: `SalesOrdersController` and `BillsController`.
- Class-level attributes: `[ApiController]`, `[Authorize]`, `[RequireModulePermission("sales")]`.
- Automatic HTTP method permission resolution:
  - GET -> `sales.view`
  - POST / PUT / PATCH -> `sales.edit`
  - DELETE -> `sales.delete`
- Method-level permission action overrides:
  - `[HttpPost("{id}/post")] [PermissionAction("approve")]`  requires `sales.approve`
  - `[HttPost("{id}/void")] [PermissionAction("void")]`  requires `sales.void`

### 4.2 Cross-Org Tenancy & Outcome Mapping
- `TenantDbContext` sets EF global query filter `OrgId == CurrentOrgId`.
- For explicit cross-org ID validation: if an invoice ID belongs to another `OrgId`within the same customer DB, the controller returns `Forbid()` (HTTP 403 Forbidden).
- Standard `InvoiceResult` outcome mapping:
  - `Ok` -> 200 OK / 201 Created / 204 NoContent
  - `NotFound` -> 404 NotFound
  - `Forbidden` -> 403 Forbid
  - `LifecycleRefused`, `LineInvalid`, `PlaceOfSupplyRefused` -> 400 BadRequest (`MessageResponse`)
  - `InsufficientStock`, `AlreadyCredited` -> 409 Conflict (`MessageResponse`)
  - `RatesUnavailable`, `PostingRefused` -> 503 ServiceUnavailable (`MessageResponse`)

---

## 5. Test Infrastructure & Recommended Test Suite

### 5.1 Test Frameworks and Harnesses
- `Shared.Kernel.Tests`: Pure unit tests (`GstCalculatorFixtureTests`, `DocumentLifecycleTests`, `NumberFormatTests`).
- `Sales.Api.Tests`: Postgres-backed tests via `PostgresFixture` / `PostgresCollection` for RLS, global query filters, schema constraints (`chk_invoicedetails_base_quantity`, `IX_InvoiceDetails_Line`).
- `Purchase.Api.Tests` (`BillServiceTests`): Model for full end-to-end service testing with `RecordingInventory` and `RecordingLedger` mocks.

3## 5.2 Recommended Test Cases for Stage T3.1 (tInvoices)
1. **Tax & Line Calculation Tests**:
   - Intra-state (CGST + SGST) vs Inter-state (IGST) calculations.
   - Price-inclusive (MRP) taxable amount and tax back-derivation.
   - Multi-rate line aggregations and round-off precision.
2. **Posting Engine Balance & GL Verification**:
   - Assert `DebitOf(AR) == CreditOf(Sales) + CreditOf(OutputGST) + RoundOff`.
   - Assert COGS & Inventory legs for direct invoice: `DebitOf(COGS) == CreditOf(Inventory)`.
   - Assert COGS & GDNI legs for challan-derived invoice: `DebitOf(COGS) == CreditOf(GDNI)`.
   - Sub-account verification: AR leg carries customer `ContactId` (`SubAccountReferenceType = 1`).
   - Output GST legs carry tax rate ID (`SubAccountReferenceType = 3`) and correct component.
3. **Lifecycle & Immutability Tests**:
   - Editing draft updates lines and totals.
   - Editing a posted invoice fails with `AlreadyPosted` / `LifecycleRefused`.
   - Posting an empty invoice (0 lines) fails with `NoLines`.
   - Voiding posted invoice withdraws GL entries and removes SalesRegister records.
4. **Multi-Tenancy & Authorization Tests**:
   - Cross-org query isolation: Org A cannot view Org B invoices.
   - Attempting to access Org B invoice by ID returns HTTP 403 Forbidden.
   - Endpoint permission tests: Missing `sales.approve` on `/post` or `sales.void` on `/void` returns 403 Forbidden.
