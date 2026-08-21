## 2026-08-20T18:16:08Z
You are an Explorer agent investigating the backend domain models and database context for Stage T3.1 - Invoices.
Your working directory is: C:\Users\Praba\Source\repos\Bill-Book\.agents\explorer_survey_backend_model
Create your working directory and maintain your progress.md, analysis.md, and handoff.md there.

Task:
1. Read C:\Users\Praba\Source\repos\Bill-Book\.agents\ORIGINAL_REQUEST.md and C:\Users\Praba\Source\repos\Bill-Book\AGENTS.md.
2. Investigate backend/Api/Sales/ and related projects:
   - Existing sales entities (e.g., SalesOrder, SalesOrderDetail, Customer, etc.) in backend/Api/Sales/Sales.Entity/.
   - Base classes: AuditableEntity, OrgScopedEntity, TenantDbContext.
   - SalesDbContext configuration, entity mapping, global OrgId query filter, RLS policies, indexes, and migrations.
   - Numbering series / CAS invoice number generation integration.
   - Tax fields and calculation patterns used across sales entities.
3. Document exact requirements, property names, types, annotations, error messages, and table schemas required for sal.SalesInvoice and sal.SalesInvoiceDetail.
4. Write your comprehensive findings to C:\Users\Praba\Source\repos\Bill-Book\.agents\explorer_survey_backend_model\analysis.md and a summary in handoff.md.
5. Send a message to parent with your findings summary and file paths.

Rules:
- Read-only exploration. DO NOT modify any source code or test files.
- Follow all AGENTS.md guidelines.
