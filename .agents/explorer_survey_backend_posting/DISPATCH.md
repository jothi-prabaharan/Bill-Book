## 2026-08-20T18:16:08Z
You are an Explorer agent investigating the backend posting service, accounting/inventory integration, controllers, and test infrastructure for Stage T3.1 - Invoices.
Your working directory is: C:\Users\Praba\Source\repos\Bill-Book\.agents\explorer_survey_backend_posting
Create your working directory and maintain your progress.md, analysis.md, and handoff.md there.

Task:
1. Read C:\Users\Praba\Source\repos\Bill-Book\.agents\ORIGINAL_REQUEST.md and C:\Users\Praba\Source\repos\Bill-Book\AGENTS.md.
2. Investigate backend services and integration points:
   - Accounting engine / GL: acc.JournalLedger, double-entry balanced debit/credit rows, ChartOfAccounts, AccountMovementSource, posting transaction patterns.
   - Inventory integration: stock depletion mechanisms, stock movements/transactions.
   - Invoice posting flow: transaction boundaries, status transitions (Draft -> Posted, Voided), CAS invoice number generation, immutability enforcement, GL reversal for voiding.
   - Controller patterns: SalesOrdersController, permission attributes ([RequireModulePermission]), authorization ([Authorize]), OrgId validation (403 Forbid), error handling.
   - Test infrastructure: backend test projects, unit test patterns for tax calculations, posting engine balance, RLS isolation, org validation.
3. Write your comprehensive findings to C:\Users\Praba\Source\repos\Bill-Book\.agents\explorer_survey_backend_posting\analysis.md and a summary in handoff.md.
4. Send a message to parent with your findings summary and file paths.

Rules:
- Read-only exploration. DO NOT modify any source code or test files.
- Follow all AGENTS.md guidelines.
