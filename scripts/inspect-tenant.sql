-- Every table in the tenant database with an exact row count, so an empty
-- schema is obvious. query_to_xml runs the count per table; reltuples would
-- only be an estimate and reads 0 until ANALYZE has run.
\echo '-- Schemas present:'
SELECT n.nspname AS schema, count(c.oid) AS tables
FROM pg_namespace n
LEFT JOIN pg_class c ON c.relnamespace = n.oid AND c.relkind = 'r'
WHERE n.nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast')
GROUP BY n.nspname ORDER BY n.nspname;

\echo ''
\echo '-- Tables holding seed data (rows > 0):'
SELECT table_schema AS schema, table_name AS "table",
       (xpath('/row/cnt/text()',
              query_to_xml(format('select count(*) as cnt from %I.%I', table_schema, table_name),
                           false, true, '')))[1]::text::bigint AS rows
FROM information_schema.tables
WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
  AND table_type = 'BASE TABLE'
  AND (xpath('/row/cnt/text()',
             query_to_xml(format('select count(*) as cnt from %I.%I', table_schema, table_name),
                          false, true, '')))[1]::text::bigint > 0
ORDER BY 1, 2;

\echo ''
\echo '-- Empty tables, by schema (a fully unseeded schema shows every table here):'
SELECT table_schema AS schema, count(*) AS empty_tables
FROM information_schema.tables
WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
  AND table_type = 'BASE TABLE'
  AND (xpath('/row/cnt/text()',
             query_to_xml(format('select count(*) as cnt from %I.%I', table_schema, table_name),
                          false, true, '')))[1]::text::bigint = 0
GROUP BY 1 ORDER BY 1;
