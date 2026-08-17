-- Diagnoses why signup leaves a customer stuck, and whether the tenant database
-- was fully built with its seed data.
--
--     psql -h localhost -U postgres -d postgres -f scripts/diagnose-tenant.sql
--
-- Read-only. Answers four questions in order:
--   1. Which databases exist at all.
--   2. What the admin database thinks happened to each signup.
--   3. Whether EP_Tenant_Template is complete — it is cloned for every new
--      tenant, so anything missing here is missing from every customer.
--   4. Whether the customer's own database actually carries the seeded rows.

\echo '=================================================================='
\echo ' 1. Databases'
\echo '=================================================================='
SELECT datname AS database,
       pg_encoding_to_char(encoding) AS encoding,
       datistemplate AS is_template,
       pg_size_pretty(pg_database_size(datname)) AS size
FROM pg_database
WHERE datname NOT IN ('postgres', 'template0', 'template1')
ORDER BY datname;

\echo ''
\echo '=================================================================='
\echo ' 2. Signups and their provisioning status (EP_Admin)'
\echo '=================================================================='
\c EP_Admin

\echo '-- Customers:'
SELECT "CustomerId", "CustomerCode", "CompanyName", "Status", "CreatedAt"
FROM mst."Customers" ORDER BY "CreatedAt" DESC LIMIT 10;

\echo ''
\echo '-- Databases provisioned (Status 0=Pending 1=InProgress 2=Ready 3=Failed):'
SELECT "CustomerId", "DatabaseName", "Status", "ProvisionedAt"
FROM mst."CustomerDatabases" ORDER BY "CustomerId";

\echo ''
\echo '-- Organizations (Status 0=Trial 1=Active ...):'
SELECT "OrgId", "CustomerId", "OrgCode", "OrgName", "Status"
FROM mst."Organizations" ORDER BY "OrgId";

\echo ''
\echo '=================================================================='
\echo ' 3. EP_Tenant_Template — cloned for every new tenant'
\echo '=================================================================='
\c EP_Tenant_Template

\echo '-- Schemas present, with table counts. Expect con, acc, inv, sal, pur:'
SELECT n.nspname AS schema, count(c.oid) AS tables
FROM pg_namespace n
LEFT JOIN pg_class c ON c.relnamespace = n.oid AND c.relkind = 'r'
WHERE n.nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast')
GROUP BY n.nspname
ORDER BY n.nspname;

\echo ''
\echo '-- Migrations applied to the template:'
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
