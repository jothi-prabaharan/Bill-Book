# Project: Stage T3.1 — Invoices (INV)

## Architecture
- **Multi-Tenancy & Security**: Database per customer, `OrgId` branch isolation with EF Core global query filters in `TenantDbContext` and PostgreSQL Row-Level Security (RLS) policies.
- **Backend Architecture**: .NET 10 Web API modular monolith in `backend/Api/Sales/`.
  - Entities inherit `DocumentHeaderBase`, `DocumentLineBase`, and `DocumentLineTaxBase` (which extend `OrgScopedEntity` and `AuditableEntity`).
  - Strict LINQ/EF Core only (no raw SQL).
  - Clean transaction boundaries: Invoices posted via `InvoiceService` generating CAS number, balanced GL journal entries in `acc.JournalLedger` via `ILedgerClient`, inventory depletion via `IInventoryClient`, and tax records in `sal.SalesRegister`.
  - Immutable once `Posted` or `Voided`; voiding executes reversing GL entries.
- **Frontend Architecture**: Angular 20 standalone components in `libs/sales/sales-ui`, `libs/sales/sales-core`, and `apps/web`.
  - Strict use of `inject()`, `signal()`, `computed()`, and `async`/`await`.
  - Scaled integer paise & 6-decimal quantity calculations via `document-line-scale.ts`.
  - Visual GL Breakdown preview prior to posting.
  - Fully responsive design (~360px mobile breakpoint), CSS variables, Bootstrap utility classes, FontAwesome icons.
  - Field-level validation on inputs, shared message banners for GL/inventory errors.

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | Invoice Entities & DbContext | `sal.Invoice`, `sal.InvoiceDetail`, `sal.InvoiceDetailTax` entities, EF Core configurations, check constraints, OrgId filter & RLS | M1 | Survey |
| 2 | Invoice Service & Lifecycle Engine | Draft save, CAS numbering ("INV"), GstCalculator tax computation, immutability on Posted/Void | M1 | Survey |
| 3 | Accounting GL Double-Entry Posting | Integration with `ILedgerClient.PostAsync` generating balanced Dr Accounts Receivable, Cr Sales, Cr Output GST, Dr/Cr Roundoff | M2 | Survey |
| 4 | Inventory Stock Depletion | Integration with `IInventoryClient.IssueAsync` with `ReleaseReservation` on SO conversion; COGS/Inventory accounting legs | M2 | Survey |
| 5 | Invoices Controller & Authorization | `SalesInvoicesController` / `InvoicesController` with `[Authorize]`, `[RequireModulePermission("sales")]`, `[PermissionAction]`, cross-org 403 Forbid | M2 | Survey |
| 6 | Frontend Invoice Form & Workflows | Angular 20 signal-based form, Customer/Item lookups, Direct creation, SO conversion (`?salesOrderId=...`), Draft/Post/Void | M3 | Survey |
| 7 | Visual GL Breakdown Preview Component | Real-time preview of Debit/Credit legs with balanced indicator before final posting | M3 | Survey |
| 8 | Invoice List, View, Print & PDF Layout | Responsive list, GST Tax Invoice printable layout, ~360px mobile responsiveness | M3 | Survey |
| 9 | Validation & Error Handling UI | Field-level error messages directly on inputs, message box banner for GL/inventory errors | M3 | Survey |
| 10 | Backend Unit & Integration Tests | Unit tests for tax math, posting engine balance, RLS isolation, cross-org 403, posted immutability | M4 | Survey |
| 11 | User Documentation & Release Notes | `frontend/apps/docs/content/invoices.md`, `docs.manifest.ts`, `release-notes.md` | M4 | Survey |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Backend Domain Models & Service Foundation | Entities, DbContext mappings, DTO models, `InvoiceService` core CRUD & tax math | none | DONE |
| M2 | Backend GL/Inventory Integration & Controller | GL posting, inventory depletion, void reversals, `InvoicesController` with auth & cross-org checks | M1 | DONE |
| M3 | Frontend UI, Workflows & GL Preview | Standalone Angular 20 form, list, GL preview, print view, responsive design, validations | M2 | IN_PROGRESS |
| M4 | Comprehensive Testing, Docs & Release | Backend integration tests, `npm run check`, user docs, manifest, release notes | M3 | PLANNED |

## Interface Contracts
### Sales Invoice DTOs & Service Contract
- `SaveInvoiceRequest`: `Guid? CustomerId`, `DateTime DocumentDate`, `DateTime? DueDate`, `Guid? SalesOrderId`, `Guid? DeliveryChallanId`, `Guid? PaymentTermId`, `string? CustomerNotes`, `string? TermsAndConditions`, `List<SaveInvoiceLineRequest> Lines`.
- `SaveInvoiceLineRequest`: `Guid? ItemId`, `string Description`, `string? HsnSacCode`, `Guid? WarehouseId`, `Guid? UomId`, `decimal Quantity`, `decimal ConversionFactor`, `decimal UnitPrice`, `bool IsPriceInclusive`, `decimal DiscountPercentage`, `decimal DiscountAmount`, `int TaxTreatment`, `int? AccountId`.
- `InvoiceView`: `Guid InvoiceId`, `Guid OrgId`, `string DocumentNo`, `DateTime DocumentDate`, `DateTime? DueDate`, `DocumentStatus Status`, `Guid? CustomerId`, `string? CustomerName`, `string? CustomerGstin`, `decimal Subtotal`, `decimal TaxAmount`, `decimal RoundOff`, `decimal TotalAmount`, `List<InvoiceLineView> Lines`, `List<InvoiceTaxView> Taxes`.
- `GlPreviewResult`: `List<GlEntryLegView> Legs`, `decimal TotalDebit`, `decimal TotalCredit`, `bool IsBalanced`.

### Controller Endpoints
- `GET /api/sales/invoices` -> `PagedResult<InvoiceSummary>`
- `GET /api/sales/invoices/{id}` -> `InvoiceView`
- `GET /api/sales/invoices/{id}/gl-preview` -> `GlPreviewResult`
- `POST /api/sales/invoices` -> `InvoiceResult`
- `PUT /api/sales/invoices/{id}` -> `InvoiceResult`
- `POST /api/sales/invoices/{id}/post` -> `InvoiceResult`
- `POST /api/sales/invoices/{id}/void` -> `InvoiceResult` (body: `VoidRequest { Reason }`)

## Code Layout
- Backend Domain Entities: `backend/Api/Sales/Sales.Entity/TableEntities/Invoice.cs`, `InvoiceDetail.cs`, `InvoiceDetailTax.cs`
- Backend DTOs & Models: `backend/Api/Sales/Sales.Entity/InvoiceModels.cs`
- Backend Repository & DbContext: `backend/Api/Sales/Sales.Repository/SalesDbContext.cs`
- Backend Services: `backend/Api/Sales/Sales.Api/Services/InvoiceService.cs`, `InvoicePostingService.cs`
- Backend Controllers: `backend/Api/Sales/Sales.Api/Controllers/InvoicesController.cs`
- Backend Tests: `backend/Tests/Sales.Api.Tests/InvoiceServiceTests.cs`, `InvoicePostingTests.cs`, `InvoiceIsolationTests.cs`
- Frontend Models & Core: `frontend/libs/sales/sales-core/src/lib/models/sales-invoice.model.ts`, `document-line-scale.ts`
- Frontend Services: `frontend/libs/sales/sales-core/src/lib/services/sales.service.ts`
- Frontend UI: `frontend/libs/sales/sales-ui/src/lib/invoice-form/`, `invoice-gl-preview/`, `invoice-view/`, `invoice-print/`
- Documentation: `frontend/apps/docs/content/invoices.md`, `frontend/apps/docs/src/app/docs.manifest.ts`, `frontend/apps/docs/content/releases.md`, `release-notes.md`
