## 2026-08-19T14:45:04Z
You are the API Spec Miner for the Bill-Book application.
Your working directory is `C:\Users\Praba\Source\repos\Bill-Book\.agents\spec_miner_api_1`.
You MUST create your directory if it does not exist, maintain your `progress.md` and `BRIEFING.md` in your directory, and write your final findings to `C:\Users\Praba\Source\repos\Bill-Book\.agents\spec_miner_api_1\analysis.md`.
When finished, send a handoff message to parent (`81ce1b4e-8b82-482d-87dd-d3c3263fc136` / orchestrator) with a summary and the path to `analysis.md`.

MANDATORY INPUTS TO READ:
1. `C:\Users\Praba\Source\repos\Bill-Book\.agents\ORIGINAL_REQUEST.md`
2. Backend code: `backend/Api`, `backend/src/` or related projects
3. Postman collection: `postman/Bill-Book.postman_collection.json`
4. Rules: `docs/coding-standards.md`, `docs/ai-agent-structure-rules.md`, `AGENTS.md`

TASKS:
1. Investigate the backend API contracts and Postman collection for all modules:
   - Sales module (Sales Invoices, Quotations, Orders, etc.): List endpoints, pagination/sorting/filtering query params, DTO fields, Create/Edit request DTO fields, validation rules, required fields.
   - Purchases module: List & Create/Edit DTOs.
   - Inventory / Items module: List & Create/Edit DTOs.
   - Accounting / Accounts module: List & Create/Edit DTOs. (Confirm UI label is strictly "Accounts", verify endpoint paths and DTOs).
   - Contacts / Customers / Vendors module: List & Create/Edit DTOs.
   - Settings / Organizations / Financial Year endpoints for Topbar dropdowns.
2. For each module, document:
   - List API: HTTP method, URL pattern, Query parameters, Response schema (items list, total count, page size, page number).
   - Create/Edit API: HTTP method, URL pattern, Request body schema with exact field names and types, Response schema.
3. Map out the frontend service and model requirements for Sales and other modules so reactive forms exactly mirror backend request DTOs.

Write full, detailed analysis in `C:\Users\Praba\Source\repos\Bill-Book\.agents\spec_miner_api_1\analysis.md`.
