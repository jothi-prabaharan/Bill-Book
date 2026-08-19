## 2026-08-19T15:09:29Z

You are the Explorer for Milestone 3: App Shell Decomposition (`libs/app-shell`).
Your working directory is `C:\Users\Praba\Source\repos\Bill-Book\.agents\explorer_m3_1`.
You MUST create your directory if it does not exist, maintain your `progress.md` and `BRIEFING.md` in your directory, and write your findings to `C:\Users\Praba\Source\repos\Bill-Book\.agents\explorer_m3_1\analysis.md`.
When finished, send a handoff message to parent (`81ce1b4e-8b82-482d-87dd-d3c3263fc136` / orchestrator).

MANDATORY INPUTS TO READ:
1. `C:\Users\Praba\Source\repos\Bill-Book\.agents\ORIGINAL_REQUEST.md`
2. `C:\Users\Praba\Source\repos\Bill-Book\PROJECT.md`
3. Design Reference: `C:\Users\Praba\Downloads\Claude Design\Bill-Book Design-handoff\bill-book-design\project\Shell.dc.html`
4. Target library: `frontend/libs/app-shell/`

TASKS:
1. Inspect existing `frontend/libs/app-shell/` files.
2. Formulate the decomposition into 4 distinct standalone components:
   - `ShellComponent` (`bb-shell`): Root CSS-grid container (`grid-template-columns: 56px 1fr; grid-template-rows: 46px auto 1fr`), holding rail, topbar, breadcrumbs, content outlet, and mobile responsiveness.
   - `ShellNavComponent` (`bb-shell-nav`): 56px fixed left rail (`z-index: 5`), module routes, active item cutout rule with 4px left accent rule, bottom user profile menu.
   - `ShellTopbarComponent` (`bb-shell-topbar`): 46px sticky top bar (`z-index: 6`), searchable org dropdown with filter, display-only FY tag, action group buttons (`New`, `Favourites`, `Help`, `Sign out`).
   - `ShellBreadcrumbComponent` (`bb-shell-breadcrumb`): Sticky breadcrumb strip (`z-index: 4`), replaces `<h1>` headings, dynamic path resolution from URL, right-aligned module action host.
3. Ensure `libs/app-shell/src/index.ts` cleanly exports `ShellComponent`, `ShellNavComponent`, `ShellTopbarComponent`, `ShellBreadcrumbComponent`.
4. Verify that the UI label for `accounting` is strictly **Accounts** ("Accounting" must never appear).
5. Prepare implementation blueprints for the worker.
Write detailed report in `analysis.md` and send handoff.
