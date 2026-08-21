# BRIEFING — 2026-08-20T18:46:11Z

## Mission
Implement Milestone 3: Frontend Construction (libs/sales & apps/web) for Stage T3.1 - Invoices in RetailErp.

## 🔒 My Identity
- Archetype: worker
- Roles: implementer, qa, specialist
- Working directory: C:\Users\Praba\Source\repos\Bill-Book\.agents\worker_m3_frontend_ui
- Original parent: 01f9a570-97e1-4f48-a358-a0c24fb12427
- Milestone: Milestone 3 - Invoices Frontend Construction

## 🔒 Key Constraints
- Standalone components only in Angular 20.
- `inject()`, `signal()`, `computed()`, and `async`/`await`.
- Separate `templateUrl` and `styleUrl`.
- File suffixes: `.page.ts`, `.dialog.ts`, `.list.ts`, `.component.ts`.
- `-core` libs stay Ionic-compatible.
- Every page works at ~360px.
- No new packages.
- Scaled line grid arithmetic via `document-line-scale.ts` and `toGridLine` / `toApiLine`.
- Pure LINQ on backend (if applicable, though here write scope is frontend).
- Field-level validation on inputs (`.field-error`) and shared message banner (`.banner--error`).

## Current Parent
- Conversation ID: 01f9a570-97e1-4f48-a358-a0c24fb12427
- Updated: 2026-08-20T18:46:11Z

## Task Summary
- **What to build**: Full Sales Invoice frontend: models & service in `sales-core`, `InvoiceFormComponent`, `InvoiceViewComponent` / print layout, `InvoiceGlPreviewComponent` / breakdown, `SalesListComponent` invoice tab/actions, routing in `sales.routes.ts`, navigation links in `apps/web`.
- **Success criteria**: Genuine UI workflows (Direct create, Convert from SO, Draft, Post, Void, GL Preview, Print), clean typecheck, unit tests passing.
- **Interface contracts**: `docs/Specification.md`, `explorer_survey_frontend_ui/analysis.md`.
- **Code layout**: `frontend/libs/sales/`, `frontend/apps/web/`.

## Key Decisions Made
- [Initial startup]: Reading all project documents and existing sales/purchase UI patterns.

## Artifact Index
- `.agents/worker_m3_frontend_ui/progress.md` — Progress tracker and liveness heartbeat
- `.agents/worker_m3_frontend_ui/handoff.md` — Handoff report

## Change Tracker
- **Files modified**: None yet
- **Build status**: Pending initial run
- **Pending issues**: None

## Quality Status
- **Build/test result**: Pending
- **Lint status**: Clean
- **Tests added/modified**: Pending

## Loaded Skills
- None
