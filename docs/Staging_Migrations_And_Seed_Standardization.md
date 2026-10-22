# Staging Migrations and Seed Data Standardization

Multi-tenant microservice database migration management and seed data standardization for Bill-Book.

## 1. Tenancy Model & Database Layout

- **Database-per-Customer / Trial Option**: Dedicated database per tenant (e.g. `IN000001`, `CustomerDb_{id}`), plus a starter/trial database (`IN000001`).
- **Schema-per-Service**: Each microservice owns an isolated schema (`acc`, `pur`, `sal`, `inv`, `cus`, `mst`, `con`, `hrm`, `adm`, `att`, `clm`, `fac`, `fee`, `amc`, `pay`, `prf`, `ppm`, `prt`, `rec`, `rpt`, `sit`, `sis`, `tla`, `wrk`, `gwy`, `ntf`).
- **History Isolation**: `__EFMigrationsHistory` is explicitly scoped to the service schema (`x.MigrationsHistoryTable("__EFMigrationsHistory", "schema")`).

## 2. Dynamic JSON Seed Loader

- [`IJsonSeedLoader`](../backend/shared/Shared.Kernel/Seed/IJsonSeedLoader.cs) and [`JsonSeedLoader`](../backend/shared/Shared.Kernel/Seed/JsonSeedLoader.cs) in `Shared.Kernel`.
- Reads structured JSON arrays from `SeedData/` directory.
- Matches entities by CLR type name or table name (supports pluralization and English irregulars).
- Compiles expression trees to extract composite keys, unique indexes, or natural keys (`*Code`, `*SystemName`, `Priority`, `Name`).
- Bypasses query filters (`IgnoreQueryFilters()`) to ensure idempotent loading without duplicates.
- Automatically populates `OrgId` on `OrgScopedEntity` records.

## 3. Per-Request Multi-Tenant Database Migration Middleware

- [`TenantMigrationMiddleware<TContext>`](../backend/shared/Shared.Kernel/Tenancy/TenantMigrationMiddleware.cs) in `Shared.Kernel.Tenancy`.
- Resolves tenant DB across `ITenantContext`, JWT claims (`database_name`, `db_name`, `plan_tier = Trial`), header (`X-Database-Name`), and default fallback (`IN000001`).
- **Fast Path**: `VerifiedCache` in-memory dictionary gives $O(1)$ lock-free throughput for already-verified databases.
- **Tenant Locks**: Per-tenant `SemaphoreSlim` serializes simultaneous requests for the same tenant without blocking other tenants.
- **Resilient Execution**: Uses `StartupMigration.MigrateAsync` with timeout expansion and transient retries.
- **Auto Seeding**: Post-migration execution of dynamic JSON seed loader.

## 4. Reset & Baseline Scripts

- `scripts/reset-migrations.ps1` (PowerShell)
- `scripts/reset-migrations.sh` (Bash)
- Support `-Clean` (removes legacy migrations) and `-AddBaseline` (generates fresh `InitialBaseline` migrations across all 26 contexts).
