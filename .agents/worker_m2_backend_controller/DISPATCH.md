## 2026-08-20T18:35:00Z
You are the Worker agent implementing Milestone 2: Backend GL/Inventory Integration & InvoicesController for Stage T3.1 - Invoices.
Your working directory is: C:\Users\Praba\Source\repos\Bill-Book\.agents\worker_m2_backend_controller
Maintain your progress.md and handoff.md in your working directory.

Context & Inputs:
- Read C:\Users\Praba\Source\repos\Bill-Book\.agents\ORIGINAL_REQUEST.md
- Read C:\Users\Praba\Source\repos\Bill-Book\AGENTS.md
- Read C:\Users\Praba\Source\repos\Bill-Book\.agents\orchestrator_inv_1\PROJECT.md
- Read survey reports:
  - C:\Users\Praba\Source\repos\Bill-Book\.agents\explorer_survey_backend_posting\analysis.md
  - C:\Users\Praba\Source\repos\Bill-Book\.agents\explorer_survey_backend_posting\handoff.md
  - C:\Users\Praba\Source\repos\Bill-Book\.agents\worker_m1_backend_model\handoff.md

Write Ownership (Exclusive):
- backend/Api/Sales/Sales.Api/Controllers/InvoicesController.cs
- backend/Api/Sales/Sales.Api/Services/InvoiceService.cs
- backend/Api/Sales/Sales.Api/Services/IInvoiceService.cs
- backend/Tests/Sales.Api.Tests/InvoicesControllerTests.cs
- backend/Tests/Sales.Api.Tests/InvoicePostingTests.cs

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Scope & Tasks:
1. Ensure `InvoicesController.cs` has:
   - `[ApiController]`, `[Route("api/sales/invoices")]`, `[Authorize]`, `[RequireModulePermission("sales")]`.
   - `[PermissionAction("view")]` on GET list and GET by id.
   - `[PermissionAction("create")]` on POST save draft.
   - `[PermissionAction("edit")]` on PUT update draft.
   - `[PermissionAction("approve")]` on POST post/finalize.
   - `[PermissionAction("void")]` on POST void.
   - Strict cross-org check: if the invoice exists in another `OrgId`, return `Forbid()` (HTTP 403 Forbidden).
   - Endpoints:
     - `GET /api/sales/invoices` -> `PagedResult<InvoiceSummary>`
     - `GET /api/sales/invoices/{id}` -> `InvoiceView`
     - `GET /api/sales/invoices/{id}/gl-preview` -> `GlPreviewResult`
     - `POST /api/sales/invoices` -> `InvoiceResult`
     - `PUT /api/sales/invoices/{id}` -> `InvoiceResult`
     - `POST /api/sales/invoices/{id}/post` -> `InvoiceResult`
     - `POST /api/sales/invoices/{id}/void` -> `InvoiceResult` (accepting `VoidInvoiceRequest { Reason }`)
2. Verify that `InvoiceService.cs` properly handles GL double-entry posting (`ILedgerClient`), inventory depletion with reservation release when from sales order (`IInventoryClient`), and `SalesRegister` population.
3. Add unit and integration tests in `InvoicesControllerTests.cs` and `InvoicePostingTests.cs`:
   - Verify 403 Forbidden on cross-org access.
   - Verify immutability rejection on trying to edit a Posted invoice.
   - Verify balanced GL preview and post ledger entries.
   - Verify voiding creates reversing entries and marks status Void.
4. Verify your work with `dotnet build` and `dotnet test`.
5. Write your handoff report to `C:\Users\Praba\Source\repos\Bill-Book\.agents\worker_m2_backend_controller\handoff.md` and message parent when complete.
