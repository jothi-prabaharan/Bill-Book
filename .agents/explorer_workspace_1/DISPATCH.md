## 2026-08-19T14:45:04Z

User Request:
You are the Nx Workspace Explorer for the Bill-Book application.
Your working directory is `C:\Users\Praba\Source\repos\Bill-Book\.agents\explorer_workspace_1`.
You MUST create your directory if it does not exist, maintain your `progress.md` and `BRIEFING.md` in your directory, and write your final findings to `C:\Users\Praba\Source\repos\Bill-Book\.agents\explorer_workspace_1\analysis.md`.
When finished, send a handoff message to parent (`81ce1b4e-8b82-482d-87dd-d3c3263fc136` / orchestrator) with a summary and the path to `analysis.md`.

MANDATORY INPUTS TO READ:
1. `C:\Users\Praba\Source\repos\Bill-Book\.agents\ORIGINAL_REQUEST.md`
2. Rules: `docs/coding-standards.md`, `docs/ai-agent-structure-rules.md`, `docs/project-structure.md`, `AGENTS.md`, `docs/commit-rules.md`
3. Angular workspace: `frontend/` directory, `frontend/package.json`, `frontend/nx.json`, `frontend/tsconfig.base.json`

TASKS:
1. Map the existing Angular Nx workspace structure:
   - Check existing libraries under `frontend/libs/` or `frontend/apps/` (specifically `shared/theming`, `shared/ui-components`, `libs/app-shell`, `sales-ui`, `sales-core`, `accounting-ui` / `accounts-ui`, `purchases-ui`, `inventory-ui`, `contacts-ui`, etc.).
   - Note which libs already exist, their paths, project names, tsconfig paths, and dependencies.
2. Check existing components, services, models in these libraries.
3. Check the web app entry point (`frontend/apps/web/` or similar), routing configuration, app.component, bootstrap.
4. Verify Angular version (Angular 20 / standalone components / signals / `inject()` usage).
5. Identify all package scripts for build/lint/test (e.g. `npx nx run-many -t lint,test`, `npx nx build web`).
6. Identify architectural boundary rules (no importing `-ui` from `-core`, no cross-module imports except via `shared/*`, presentation in `shared/ui-components`, shell chrome in `app-shell`, tokens in `shared/theming`).
7. Confirm that "Accounting" must never appear as a UI string, but directory name `accounting` remains as is if already named.
