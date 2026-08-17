\echo '-- Customers:'
\x on
SELECT * FROM mst."Customers" ORDER BY "CustomerId";
\x off
\echo ''
\echo '-- CustomerDatabases:'
SELECT * FROM mst."CustomerDatabases" ORDER BY "CustomerId";
\echo ''
\echo '-- Organizations:'
\x on
SELECT * FROM mst."Organizations" ORDER BY "OrgId";
\x off
