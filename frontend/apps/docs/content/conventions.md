# Conventions

The binding rules live in `CLAUDE.md` at the repository root. This page summarises the ones you will hit first.

## Data access

- **LINQ only.** The only raw SQL permitted is `CREATE DATABASE`, RLS policies, triggers and `set_config` — everything else has a LINQ equivalent and must use it.
- **PostgreSQL only.** RLS, `xmin` and JSONB are used deliberately; do not add SQL Server compatibility or avoid a Postgres feature for portability.
- **PascalCase tables and columns**, matching the C# property names exactly. Postgres needs quoted identifiers for that, which is expected.

## Entities

- Plain property bags — no constructors, no methods, no computed properties, no validation logic
- **Every Data Annotation carries an `ErrorMessage`**
- All inherit `AuditableEntity`
- **Enums, never magic strings**, for any fixed set of values

## Audit columns

Every table has `CreatedBy`, `CreatedAt`, `ModifiedBy`, `ModifiedAt`, and **all four are nullable**. `CreatedBy IS NULL` marks system/seed data — a row written by no user. They are set only by `AuditSaveChangesInterceptor`; never assign them by hand.

## Tenancy

Every table in a per-customer schema carries `OrgId` plus a global query filter and an RLS policy. **A missing filter leaks data between organizations** — this is the highest-consequence mistake in the codebase.

Every unique constraint on an org-scoped table must include `OrgId`.

## Service boundaries

Never reference another service's `DbContext`. Read over its API through a named seam; write by publishing an event.

## Frontend components

Three files per page or component, sharing a base name: `.ts`, `.html`, `.scss`. The decorator uses `templateUrl` and `styleUrl` — **no inline `template:` or `styles:` blocks**. A component with no styles of its own still gets a `.scss` file, so the trio is the same everywhere.

`-core` libs stay Ionic-compatible: Signals and DI are fine, but no `window`, no `document`, no Syncfusion, no Electron or Node APIs. Every page works at ~360px.

## Documentation

**Update this site in the same commit as the feature.** See [Release notes](#/release-notes) for how that flows into a version entry.
