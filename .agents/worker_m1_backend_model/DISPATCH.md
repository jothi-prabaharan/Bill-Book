## 2026-08-20T18:28:56Z
You are the Worker agent implementing Milestone 1: Backend Domain Models, DbContext & Service Foundation for Stage T3.1 - Invoices.
Your working directory is: C:\Users\Praba\Source\repos\Bill-Book\.agents\worker_m1_backend_model
Maintain your progress.md and handoff.md in your working directory.

Context & Inputs:
- Read C:\Users\Praba\Source\repos\Bill-Book\.agents\ORIGINAL_REQUEST.md
- Read C:\Users\Praba\Source\repos\Bill-Book\AGENTS.md
- Read C:\Users\Praba\Source\repos\Bill-Book\.agents\orchestrator_inv_1\PROJECT.md
- Read Explorer findings:
  - C:\Users\Praba\Source\repos\Bill-Book\.agents\explorer_survey_backend_model\analysis.md
  - C:\Users\Praba\Source\repos\Bill-Book\.agents\explorer_survey_backend_model\handoff.md

Write Ownership (Exclusive):
- backend/Api/Sales/Sales.Entity/InvoiceModels.cs
- backend/Api/Sales/Sales.Entity/TableEntities/Invoice.cs
- backend/Api/Sales/Sales.Entity/TableEntities/InvoiceDetail.cs
- backend/Api/Sales/Sales.Entity/TableEntities/InvoiceDetailTax.cs
- backend/Api/Sales/Sales.Repository/SalesDbContext.cs
- backend/Api/Sales/Sales.Api/Services/InvoiceService.cs
- backend/Api/Sales/Sales.Api/Services/IInvoiceService.cs

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Scope & Tasks:
1. Ensure `Invoice`, `InvoiceDetail`, `InvoiceDetailTax` entities inherit `DocumentHeaderBase`, `DocumentLineBase`, and `DocumentLineTaxBase` and conform to plain property bags with DataAnnotations and `ErrorMessage` on each attribute.
2. Complete `InvoiceModels.cs` with full line fields (`SaveInvoiceRequest`, `SaveInvoiceLineRequest`, `InvoiceView`, `InvoiceLineView`, `InvoiceTaxView`, `InvoiceSummary`, `GlPreviewResult`, `GlEntryLegView`, `VoidInvoiceRequest`, etc.) matching `DocumentLineBase` surface (HsnSacCode, WarehouseId, UomId, ConversionFactor, IsPriceInclusive, TaxTreatment, LineType, AccountId, etc.).
3. Update `InvoiceService.cs` to implement Draft Save/Update, GetById, List, Tax calculation using `GstCalculator.Compute` and `GstCalculator.Totals`, `BaseQuantity = round(Quantity * ConversionFactor, 6)`, `LineNumber` assignment, and atomic CAS number generation via `INumberGenerator.NextAsync("INV", ...)`.
4. Ensure EF Core mapping and check constraints in `SalesDbContext` are fully configured with global query filter on `OrgId` and RLS policy compatibility.
5. Verify your work by running `dotnet build` in `backend/` and `dotnet test backend/Tests/Sales.Api.Tests/`.
6. Write a detailed handoff report to `C:\Users\Praba\Source\repos\Bill-Book\.agents\worker_m1_backend_model\handoff.md` and send a message to parent when complete.
