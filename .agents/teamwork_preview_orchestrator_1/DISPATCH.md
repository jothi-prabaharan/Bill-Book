## 2026-08-19T14:44:27Z
You are the Project Orchestrator for the Bill-Book desktop application shell and module list/create screens implementation.

## Working Directory
Your working directory is: `C:\Users\Praba\Source\repos\Bill-Book\.agents\teamwork_preview_orchestrator_1`
Maintain your `BRIEFING.md`, `plan.md`, and `progress.md` in your working directory.

## Authoritative Request
Authoritative request is recorded at: `C:\Users\Praba\Source\repos\Bill-Book\.agents\ORIGINAL_REQUEST.md`

## Task Description
Implement the Bill-Book desktop application shell and module list/create screens in the existing Angular Nx workspace by translating the provided HTML/CSS design.

Working directory: `C:\Users\Praba\Source\repos\Bill-Book`
Integrity mode: benchmark

## Verification Resources
- Design Reference: `C:\Users\Praba\Downloads\Claude Design\Bill-Book Design-handoff\bill-book-design\project\Shell.dc.html`
- Design Tokens: `C:\Users\Praba\Downloads\Claude Design\Bill-Book Design-handoff\bill-book-design\project\_ds\bill-book-6c62bbc0-6bc5-4941-b359-3208c21e8972\styles.css`
- API Contracts: `backend/Api` + `postman/Bill-Book.postman_collection.json`
- Rules: `docs/coding-standards.md`, `docs/ai-agent-structure-rules.md`, `docs/project-structure.md`, `AGENTS.md`, `docs/commit-rules.md`

## Requirements
### R1. Design Tokens (`shared/theming`)
Port the token set from the provided `styles.css` into SCSS custom properties on `:root` in the theming library. Colors apply as borders/rules/underlines, not filled blocks. Shadows are whispers. Tabular numbers for tables/figures. Focus states use a themed outline. No hard-coded hex, font names, or raw px. CSS-only interaction states.

### R2. App Shell (`libs/app-shell`)
Implement a CSS-grid layout with a fixed left rail, top bar, breadcrumb strip, and scrolling content outlet. The left rail contains module navigation and user menu. The top bar contains actions, org name searchable dropdown, and financial year tag. The breadcrumb strip replaces page titles and holds module-level controls. Emit `ShellComponent`, `ShellNavComponent`, `ShellTopbarComponent`, and `ShellBreadcrumbComponent`.

### R3. Shared Data Table (`shared/ui-components`)
Implement a reusable data table for all list screens. Must have a sticky header with an inset bottom shadow, hairline row rules, and support compact density. Inputs include columns, rows, loading state, empty template, and sorting outputs.

### R4. Module Screens (e.g., Sales and others)
For each module (in its `-ui` lib), implement a List page (filter bar, shared table, paging) and a Create/edit page (reactive form exactly mirroring the backend request DTO). Stop and verify Sales module end-to-end before proceeding to other modules.

### R5. Architecture and Placement Constraints
Presentational pieces go to `shared/ui-components`. Tokens to `shared/theming`. Shell chrome to `app-shell`. Module-specific UI to the module's `-ui` lib. Never import `-ui` from `-core`, and never import between module libs (use `shared/*`). Folder names stay as-is, but the UI label for `accounting` is **Accounts** ("Accounting" must never appear).

## Acceptance Criteria
### Build and Lint
- `npx nx run-many -t lint,test` passes cleanly for every library touched.
- `npx nx build web` passes cleanly.

### Visual and Behavior
- No user-visible "Accounting" string appears anywhere in the UI.
- No table header or shell chrome overlap occurs while scrolling any list page at compact density (verified across all implemented modules).
- No hex or px literals are used in CSS where a design token exists.
- No JS-driven animation or hover logic is used (CSS-only).

## 2026-08-19T15:15:30Z
Resume work at C:\Users\Praba\Source\repos\Bill-Book\.agents\teamwork_preview_orchestrator_1. Read handoff.md, BRIEFING.md, ORIGINAL_REQUEST.md, DISPATCH.md, and progress.md for current state. Your parent is 81ce1b4e-8b82-482d-87dd-d3c3263fc136 — use this ID for all escalation and status reporting (send_message).

Your immediate mission as Project Orchestrator Gen 2 is:
1. Initialize your working session and start your heartbeat cron.
2. Execute Milestone 2 (Shared Data Table `bb-data-table` / `bb-data-grid`), Milestone 3 (App Shell Decomposition), Milestone 4 (Sales Module Screens & E2E verification), Milestone 5 (Remaining module screens & Accounts UI string audit), and Milestone 6 (Final adversarial hardening & clean Forensic Audit).
3. Follow the Gate procedure (Worker -> 2 Reviewers -> 2 Challengers -> 1 Forensic Auditor) for each milestone.
4. When all acceptance criteria are met, deliver the final human report to the user and handoff to parent.
