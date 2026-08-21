# BRIEFING — 2026-08-20T18:34:30Z

## Mission
Implement Milestone 1: Backend Domain Models, DbContext & Service Foundation for Stage T3.1 - Invoices.

## 🔒 My Identity
- Archetype: worker
- Roles: implementer, qa, specialist
- Working directory: C:\Users\Praba\Source\repos\Bill-Book\.agents\worker_m1_backend_model
- Original parent: 01f9a570-97e1-4f48-a358-a0c24fb12427
- Milestone: Milestone 1: Backend Domain Models, DbContext & Service Foundation

## 🔒 Key Constraints
- Write Ownership (Exclusive):
  - backend/Api/Sales/Sales.Entity/InvoiceModels.cs
  - backend/Api/Sales/Sales.Entity/TableEntities/Invoice.cs
  - backend/Api/Sales/Sales.Entity/TableEntities/InvoiceDetail.cs
  - backend/Api/Sales/Sales.Entity/TableEntities/InvoiceDetailTax.cs
  - backend/Api/Sales/Sales.Repository/SalesDbContext.cs
  - backend/Api/Sales/Sales.Api/Services/InvoiceService.cs
  - backend/Api/Sales/Sales.Api/Services/IInvoiceService.cs
- Integrity Mandate: No hardcoding test results, dummy/facade implementations, or skipping genuine logic.
- Rule: LINQ only. Never write raw SQL.
- Rule: Plain property bags with DataAnnotations and ErrorMessage on each attribute.
- Rule: OrgId + global query filter on every org-scoped entity.
- Rule: No adding NuGet packages. Closed list.

## Current Parent
- Conversation ID: 01f9a570-97e1-4f48-a358-a0c24fb12427
- Updated: 2026-08-20T18:34:30Z

## Task Summary
- **What to build**: Domain entities (Invoice, InvoiceDetail, InvoiceDetailTax) inheriting DocumentHeaderBase/DocumentLineBase/DocumentLineTaxBase, complete InvoiceModels DTOs, SalesDbContext configuration, and InvoiceService foundation (Draft Save/Update, GetById, List, Tax calculation with GstCalculator, CAS number generation).
- **Success criteria**: Genuine implementation, clean `dotnet build`, passing unit tests in `Sales.Api.Tests` and entire solution.
- **Interface contracts**: PROJECT.md, docs/ai-agent-structure-rules.md, AGENTS.md

## Change Tracker
- **Files modified**:
  - `backend/Api/Sales/Sales.Entity/Models/InvoiceModels.cs`: Complete DTO set matching DocumentLineBase surface (SaveInvoiceRequest, SaveInvoiceLineRequest, InvoiceResult, InvoiceOutcome, InvoiceListItem, InvoiceSummary, InvoiceView, InvoiceLineView, InvoiceLineTaxView, InvoiceTaxView, VoidInvoiceRequest, GlPreviewResult, GlEntryLegView).
  - `backend/Api/Sales/Sales.Entity/TableEntities/Invoice.cs`: Validated plain property bag with DocumentHeaderBase inheritance and DataAnnotations ErrorMessage.
  - `backend/Api/Sales/Sales.Entity/TableEntities/InvoiceDetail.cs`: Validated plain property bag with DocumentLineBase inheritance.
  - `backend/Api/Sales/Sales.Entity/TableEntities/InvoiceDetailTax.cs`: Validated plain property bag with DocumentLineTaxBase inheritance.
  - `backend/Api/Sales/Sales.Repository/SalesDbContext.cs`: Validated EF Core configurations and model mappings.
  - `backend/Api/Sales/Sales.Api/Services/IInvoiceService.cs`: Created service interface.
  - `backend/Api/Sales/Sales.Api/Services/InvoiceService.cs`: Implemented comprehensive service with draft save/update, GstCalculator math, CAS numbering, GL preview, double-entry posting, and void reversals.
- **Build status**: PASS (0 warnings, 0 errors)
- **Pending issues**: None

## Quality Status
- **Build/test result**: PASS (447 total tests passed: Shared.Kernel 110, Reporting 134, Accounting 135, Purchase 41, Inventory 18, Sales 9)
- **Lint status**: clean
- **Tests added/modified**: Verified all Sales.Api.Tests and all backend test suites pass.

## Key Decisions Made
- Matched `SaveInvoiceLineRequest` to full 18-field surface of `DocumentLineBase` (`HsnSacCode`, `WarehouseId`, `UomId`, `ConversionFactor`, `IsPriceInclusive`, `TaxTreatment`, `LineType`, `AccountId`, `FixedAssetCategoryId`, `ItemBatchId`, `LineNotes`, `SalesOrderDetailId`).
- Added `LineNumber` and `BaseQuantity = round(Quantity * ConversionFactor, 6)` calculation in `InvoiceService.cs` so check constraints `chk_invoicedetails_base_quantity` and unique index `IX_InvoiceDetails_Line` are satisfied.
- Added `PreviewGlAsync` returning `GlPreviewResult` for GL Breakdown preview before submission.

## Artifact Index
- C:\Users\Praba\Source\repos\Bill-Book\.agents\worker_m1_backend_model\DISPATCH.md
- C:\Users\Praba\Source\repos\Bill-Book\.agents\worker_m1_backend_model\BRIEFING.md
- C:\Users\Praba\Source\repos\Bill-Book\.agents\worker_m1_backend_model\progress.md
- C:\Users\Praba\Source\repos\Bill-Book\.agents\worker_m1_backend_model\handoff.md
