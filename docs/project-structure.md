# Project Structure

This is a multi-tenant retail ERP and accounting system.

## Root Directories
- `backend/`: The .NET 10 solution containing the API and various bounded context modules.
- `frontend/`: The client application (Angular v20/Nx monorepo style).
- `docs/`: Unified documentation containing architecture flows, module schemas, and project standards.
- `scripts/`: Powershell and SQL scripts for local developer setup (e.g., setting up the dev database and seeding it).

## Backend Modules (`backend/Api/`)
- `Accounting`
- `Customer`
- `Inventory`
- `Master`
- `Purchase`
- `Reporting`
- `Sales`

Each module generally maintains its own domain entities, EF Core DbContext, migrations, and API endpoints.

---

## Frontend (Nx + Angular) — layout and conventions

The frontend is an Nx monorepo. We use apps/ for runnable applications and libs/ for reusable code. Libraries are split into `-core` (view-models, services, models, no templates or direct DOM access) and `-ui` (presentational components and pages).

Guiding principle: apps orchestrate, -core contains behaviour and side-effects, -ui contains presentational components.

### Apps vs libs
- apps/
  - `web`, `portal`, `admin`, `desktop`, `docs` — full applications that compose libs and provide routes.
- libs/
  - `libs/{module}/{module}-core` — models, services, state, HTTP clients, facades. Must be platform-agnostic (no `window`, `document`, Node, or Electron APIs).
  - `libs/{module}/{module}-ui` — presentational components, pages and shared UI widgets.
  - `libs/shared/` — shared theme, tokens, utilities, and small wrappers for platform-specific features.

### Component folder layout
- Each reusable component lives in its own folder and contains at minimum:
  - `my-widget.component.ts`
  - `my-widget.component.html`
  - `my-widget.component.scss` (or .css)
  - `my-widget.component.spec.ts` (unit tests) or `my-widget.component.test.ts`
- Example path: `libs/{module}/{module}-ui/src/lib/my-widget/`.

### Naming conventions
- Files: kebab-case (e.g. `my-widget.component.ts`).
- Component classes: PascalCase with suffix `Component` (e.g. `MyWidgetComponent`).
- Selectors: kebab-case prefixed with `bb-` (project prefix). Example: `selector: 'bb-my-widget'`.

### Change detection & performance
- Prefer `ChangeDetectionStrategy.OnPush` for all components unless there's a documented reason not to.
- Prefer Angular Signals and pure computations in `-core` libs; use RxJS where Streams/observables are appropriate for complex async flows.
- Keep components small and focused; prefer composition over large monolithic components.

### Standalone components vs NgModules
- Prefer standalone components for small, reusable widgets and for route pages where convenient.
- Use feature modules when grouping related routes, or when a logical boundary benefits from its own module.
- Follow Nx generator defaults unless there is a strong reason to deviate.

### Container vs Presentational separation
- Container (page) components live under `apps/` and implement data fetching, permission checks and orchestration.
- Presentational components live in `libs/*-ui` and only accept Inputs/emit Outputs.
- State, HTTP calls and side-effects belong in `-core` libs or in the app's facade service. `-ui` libs must remain side-effect free.

### Inputs / Outputs best-practices
- Inputs are treated as immutable by components — never mutate input objects in-place.
- Avoid two-way binding (`[(ngModel)]`) on publicly exposed Inputs. Use `@Output()` events to communicate changes.

### Dependency injection and services
- Place domain services (API clients, facades, stores) in `-core` libs. Provide them from the app or core libs, not from `-ui` libs.
- If platform-specific APIs (window/document, printers, USB) are required, wrap them behind an injectable interface and provide the platform implementation in the app (not in `-core`).

### Lazy loading and routing
- Pages/routes should be lazy-loaded with separate route modules where it reduces initial bundle size.
- Avoid bundling unrelated pages in the same eagerly loaded module.

### Styling
- Keep component styles encapsulated. Use SCSS tokens and variables defined in `libs/shared/theme`.
- Follow the project naming convention for CSS classes (e.g., BEM or agreed project style) so global styles don't conflict.

### Accessibility (a11y)
- Interactive components must support keyboard navigation and provide appropriate ARIA attributes where necessary.
- Run automated a11y checks in CI for pages and address critical failures before merging.

### Testing & CI
- Every component should have unit tests (Vitest). Use Angular testing helpers or host-component patterns as appropriate.
- The frontend pre-check is `npm run check` which includes lint, typecheck, test and build. Run it locally before declaring a page/component as done.
- Maintain test coverage for critical UI flows (login, org-switch, invoice pages) and fix regressions.

### Documentation
- Ship documentation with any user-visible UI change. Add/update a page under `frontend/apps/docs/content/` and update `docs.manifest.ts` to include the new/changed doc.
- For public `-ui` components, include a short usage example, the Inputs/Outputs table and any required tokens/themes in the component's docs.

---

## Suggested coding role & expectations (frontend + backend)

These are short, actionable responsibilities to keep work consistent and reviewable.

- Authoring code
  - Follow repository conventions: project layout in `docs/project-structure.md` and the decisions in `CLAUDE.md`.
  - Keep each change small and self-contained. Ship documentation with the change in the same commit.
  - Run and pass required checks locally before committing: `dotnet build && dotnet test` for backend, and `npm run check` for frontend.

- Testing
  - Add unit tests and, where applicable, integration tests for new behaviour.
  - For backend that depends on Postgres features (deferred constraints, RLS), prefer tests that run against a real Postgres instance or provide a clear reason when using in-memory substitutes.

- Code reviews
  - Provide a short PR description with what changed and why, and list any follow-ups.
  - Include screenshots or brief reproduction steps for UI changes.
  - If scope grows during implementation, stop and propose a short plan before continuing.

- Commits & branches
  - Follow the project policy in `CLAUDE.md` regarding `main` as the primary branch. (If alternative branching is introduced, document the change clearly.)
  - Write clear commit messages: concise summary, followed by a short body explaining reason and impact.

- Documentation
  - Update docs for public behaviour changes (API, UI, provisioning). For UI include usage examples and expected screens.

---

## Back-end notes

(Existing backend layout notes belong in CLAUDE.md but repeated here for developer convenience)

- Each service generally has three projects: `{Module}.Entity`, `{Module}.Repository`, `{Module}.Api`.
- Dependency direction: `Api` → `Repository` → `Entity` → `Shared.Kernel`.


