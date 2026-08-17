-- In PostgreSQL, you cannot drop databases inside a PL/pgSQL "DO" block.
-- If you are using pgAdmin or a SQL editor, run this query to generate the DROP statements:
-- 
-- SELECT 'DROP DATABASE IF EXISTS "' || datname || '" WITH (FORCE);' 
-- FROM pg_database 
-- WHERE datname NOT LIKE 'template%' AND datname != 'postgres';
--
-- Then, copy the results and paste them in a new query window to execute them.

-- Alternatively, if you run this script using `psql` from the command line, 
-- it will automatically execute the drops for you using the \gexec command:

SELECT 'DROP DATABASE IF EXISTS "' || datname || '" WITH (FORCE);' 
FROM pg_database 
WHERE datname NOT LIKE 'template%' AND datname != 'postgres'


DROP DATABASE IF EXISTS "EP_Admin" WITH (FORCE);
DROP DATABASE IF EXISTS "EP_Tenant_Template" WITH (FORCE);
DROP DATABASE IF EXISTS "IN0000000001" WITH (FORCE);