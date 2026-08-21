# AI Agent Structure Rules

The following rules have been established for all AI coding assistants (including **Claude** and **Antigravity**) to ensure structural integrity across the project.

## Hard Rules
1. **LINQ only. Never write raw SQL.** The only exceptions, because no LINQ equivalent exists: `CREATE DATABASE`, RLS policies, triggers, `set_config`. Everything else — every query, insert, update, delete — is LINQ.
2. **Entities are plain property bags.** No constructors. No methods. No validation logic. No computed properties. Just `public X Y { get; set; }` with Data Annotations.
3. **Every Data Annotation needs `ErrorMessage`.**
4. **PascalCase table and column names**, matching C# property names exactly. Postgres needs quoted identifiers for this — that's expected.
5. **PostgreSQL only.** Never add SQL Server compatibility, never avoid a Postgres feature for portability. RLS, `xmin`, and JSONB are all in use deliberately.
6. **All table entities inherit `Shared.Kernel.Entities.AuditableEntity`.** Never set audit fields manually — `AuditSaveChangesInterceptor` does it.
7. **Enums, not magic strings**, for any fixed set of values.
8. **Never cross a service boundary by referencing another service's `DbContext`.** Use its API or an event.

## Adding a Table
- Entity class in `{Module}.Entity/TableEntities/{Name}.cs`
- Enums (if any) in `{Module}.Entity/Enums/`
- `DbSet` + Fluent config in `{Module}.Repository/{Module}DbContext.cs`
- Seed data if it's reference data
- Do **not** write CREATE TABLE SQL. This is EF Core code-first.
- Every per-customer table needs `OrgId` plus a global query filter.

## Adding an Endpoint
- Request/response models in `{Module}.Entity/Models/` (Data Annotations with error messages)
- Controller action in `{Module}.Api/Controllers/`
- Validate the caller's `OrgId` matches the target resource's — always
- Return `Forbid()` on cross-org access, not `NotFound()`

## Project Layout

### Backend
Three projects per service, no more — all three under `backend/Api/{Module}/`:
- `{Module}.Entity/`: TableEntities, Models, Enums
- `{Module}.Repository/`: DbContext, repositories, seed data
- `{Module}.Api/`: controllers, services, DI

Dependency direction: `Api` → `Repository` → `Entity` → `Shared.Kernel`. Never backwards.

### Frontend (Nx, Angular v20)
`apps/{web, portal, admin, desktop, docs}` 
`libs/{module}/{module}-core` (view-models + models, no templates) 
`libs/{module}/{module}-ui` (pages) 
`libs/shared/{auth, api-client, ui-components, currency-format, theming}`

### Angular Component Structure
- **Standalone Only**: Use `standalone: true`. No `NgModules` are allowed.
- **Dependency Injection**: Use the `inject()` function instead of constructor injection.
- **State & Reactivity**: Use `signal()` and `computed()` for component state over RxJS `BehaviorSubject` where possible.
- **Data Fetching**: Use `async/await` with Promises for straightforward REST calls instead of heavily piping RxJS streams.
- **File Naming**: Suffix component files accurately according to their role (`.page.ts`, `.dialog.ts`, `.list.ts`, `.component.ts`).
- **Separation of Concerns**: Use separate `templateUrl` and `styleUrl` instead of inline templates.
- **Frontend Styling**: Absolutely NO inline styles (`style="..."`). Always use global CSS classes and CSS custom properties (e.g., `var(--primary-color)`).
- **Validation UX**: Field validation errors must display directly on top of inputs. Business validation errors must display inside the shared message box component.
