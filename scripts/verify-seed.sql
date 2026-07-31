-- Row counts for the seeded master tables, so a failed seed is visible rather
-- than assumed. Run against retailerp_master.
--
-- Kept in a file rather than passed with psql -c: identifiers are PascalCase and
-- need double quotes, and PowerShell mangles those on the way to psql.exe, which
-- turns mst."Countries" into "mst.countries" and fails with "relation does not
-- exist".

\echo 'Master reference data (mst):'
SELECT 'Countries'       AS "table", count(*) AS rows FROM mst."Countries"
UNION ALL SELECT 'Currencies',       count(*) FROM mst."Currencies"
UNION ALL SELECT 'States',           count(*) FROM mst."States"
UNION ALL SELECT 'AccountTypes',     count(*) FROM mst."AccountTypes"
UNION ALL SELECT 'TransactionTypes', count(*) FROM mst."TransactionTypes"
UNION ALL SELECT 'LedgerTypes',      count(*) FROM mst."LedgerTypes"
UNION ALL SELECT 'LedgerSources',    count(*) FROM mst."LedgerSources"
UNION ALL SELECT 'HsnSacCodes',      count(*) FROM mst."HsnSacCodes"
ORDER BY 1;

\echo ''
\echo 'Identity and Platform seed data (idn, plt):'
SELECT 'Roles'           AS "table", count(*) AS rows FROM idn."Roles"
UNION ALL SELECT 'Permissions',      count(*) FROM idn."Permissions"
UNION ALL SELECT 'RolePermissions',  count(*) FROM idn."RolePermissions"
UNION ALL SELECT 'Configurations',   count(*) FROM plt."Configurations"
ORDER BY 1;
