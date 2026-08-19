## 2026-08-19T14:53:37Z

<USER_REQUEST>
You are the E2E Test Writer for the Bill-Book Desktop App Shell and Module Screens project.
Your working directory is `C:\Users\Praba\Source\repos\Bill-Book\.agents\test_writer_1`.
You MUST create your directory if it does not exist, maintain your `progress.md` and `BRIEFING.md` in your directory, and write your test suite and handoff report.
When finished, send a handoff message to parent (`81ce1b4e-8b82-482d-87dd-d3c3263fc136` / orchestrator) with the path to your handoff report and `TEST_READY.md`.

MANDATORY INPUTS TO READ:
1. `C:\Users\Praba\Source\repos\Bill-Book\.agents\ORIGINAL_REQUEST.md`
2. `C:\Users\Praba\Source\repos\Bill-Book\PROJECT.md`
3. `C:\Users\Praba\Source\repos\Bill-Book\TEST_INFRA.md`
4. Design Reference & Tokens: `C:\Users\Praba\Downloads\Claude Design\Bill-Book Design-handoff\bill-book-design\`
5. Coding Standards: `docs/coding-standards.md`, `AGENTS.md`

TASKS:
1. Design comprehensive, opaque-box test suites in the frontend workspace covering all 4 tiers:
   - Tier 1: Feature Coverage (tokens, shell layout, left rail, topbar, breadcrumbs, data table, sales list, create forms, no "Accounting" string)
   - Tier 2: Boundary & Corner Cases (empty data, max text truncation, keyboard focus, compact density, sorting edge cases)
   - Tier 3: Cross-Feature Combinations (navigation + breadcrumbs + data table filtering + routing)
   - Tier 4: Real-World Application Workflows (end-to-end sales invoice creation, list filtering, status inspection)
2. Place test files appropriately in test folders (e.g., in `frontend/libs/shared/theming/src/lib/`, `frontend/libs/app-shell/src/lib/`, `frontend/libs/shared/ui-components/src/lib/`, `frontend/libs/sales/sales-ui/src/lib/` or integration test suites).
3. Run tests using `npm run test` or `npx nx run-many -t test` to verify they compile and pass or act as precise oracles.
4. When complete, create `C:\Users\Praba\Source\repos\Bill-Book\TEST_READY.md` summarizing total test counts and tier coverage.
5. Send handoff to parent.
</USER_REQUEST>
