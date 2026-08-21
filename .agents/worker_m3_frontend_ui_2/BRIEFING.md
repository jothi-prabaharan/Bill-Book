# BRIEFING — 2026-08-21T00:20:00Z

## Mission
Construct and enhance Frontend for Sales Invoices (Milestone 3 / Stage T3.1) in libs/sales (sales-core, sales-ui) and apps/web.

## 🔒 My Identity
- Archetype: worker
- Roles: implementer, qa
- Working directory: C:\Users\Praba\Source\repos\Bill-Book\.agents\worker_m3_frontend_ui_2
- Original parent: 01f9a570-97e1-4f48-a358-a0c24fb12427
- Milestone: M3 - Frontend Construction (libs/sales & apps/web) for Stage T3.1 - Invoices

## 🔒 Key Constraints
- Standalone components only (Angular 20), inject(), signal(), computed(), async/await.
- Separate templateUrl and styleUrl (no inline templates beyond trivial).
- File suffixes: .page.ts, .dialog.ts, .list.ts, .component.ts.
- Responsive layout down to ~360px.
- No new packages in package.json.
- Full real implementation - NO CHEATING, NO hardcoding, genuine arithmetic and API interaction.

## Current Parent
- Conversation ID: 01f9a570-97e1-4f48-a358-a0c24fb12427
- Updated: not yet

## Task Summary
- **What to build**:
  - `sales-core`: models (`sales-invoice.model.ts`), service methods (`sales.service.ts`), exports (`index.ts`).
  - `sales-ui`: `InvoiceFormComponent`, `InvoiceViewComponent`, `InvoiceGlPreviewComponent`, `SalesListComponent` integration, `sales.routes.ts`, exports.
  - `apps/web`: routing & navigation integration if needed.
- **Success criteria**:
  - Direct invoice creation, conversion from Sales Order (`?salesOrderId=...`), draft save, post/finalize, void.
  - Line grid with scaled arithmetic via `document-line-scale.ts`.
  - Lookup dialogs for customer & items.
  - Real-time visual GL breakdown preview & balance verification.
  - GST tax invoice print layout.
  - Responsive mobile (~360px) design.
  - `npm run typecheck` and `npm run test` pass.

## Change Tracker
- **Files modified**: None yet
- **Build status**: Pending
- **Pending issues**: None

## Quality Status
- **Build/test result**: Pending
- **Lint status**: Pending
- **Tests added/modified**: Pending

## Key Decisions Made
- [Initial planning]

## Artifact Index
- C:\Users\Praba\Source\repos\Bill-Book\.agents\worker_m3_frontend_ui_2\progress.md
- C:\Users\Praba\Source\repos\Bill-Book\.agents\worker_m3_frontend_ui_2\handoff.md
