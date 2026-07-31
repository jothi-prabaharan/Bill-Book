-- Creates the local development and testing databases.
--
-- Run against the `postgres` maintenance database as a superuser:
--     psql -h localhost -U postgres -d postgres -f scripts/setup-dev-db.sql
--
-- Idempotent: re-running it leaves existing databases untouched. It creates no
-- schemas or tables — EF Core migrations own those. See scripts/setup-dev-db.ps1,
-- which runs this file and then applies the migrations.
--
-- CREATE DATABASE cannot run inside a transaction and has no IF NOT EXISTS, hence
-- the SELECT ... WHERE NOT EXISTS \gexec pattern: the SELECT produces the DDL text
-- only when the database is absent, and \gexec executes whatever it produced.
--
-- TEMPLATE template0 with ENCODING 'UTF8' is deliberate. template1 on a Windows
-- install is frequently WIN1252, and Tamil and Chinese text silently corrupts on
-- insert. UTF8 also makes varchar lengths count characters, not bytes.

\echo 'Creating development and testing databases...'

-- Master database: mst (countries, states, currencies), plt (customers,
-- organizations, tenant directory), idn (users, roles, tokens), rat (rates).
SELECT 'CREATE DATABASE "retailerp_master" ENCODING ''UTF8'' TEMPLATE template0'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'retailerp_master')\gexec

-- Design-time database for the per-customer schemas. `dotnet ef` needs a real
-- database to diff against, but at design time there is no authenticated request
-- and therefore no resolved tenant. Accounting's DesignTimeDatabase points here.
SELECT 'CREATE DATABASE "retailerp_design" ENCODING ''UTF8'' TEMPLATE template0'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'retailerp_design')\gexec

-- Testing database. Separate from retailerp_master so a test that truncates a
-- table cannot destroy development data. Same postgres superuser by decision.
SELECT 'CREATE DATABASE "retailerp_test" ENCODING ''UTF8'' TEMPLATE template0'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'retailerp_test')\gexec

-- A stand-in customer database. In production, provisioning issues CREATE DATABASE
-- per Customer and records it in the plt tenant directory. Locally this one exists
-- so the per-customer schemas have somewhere to live before signup has ever run;
-- it matches Accounting's TenantFallback connection string.
SELECT 'CREATE DATABASE "IN0000000001" ENCODING ''UTF8'' TEMPLATE template0'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'IN0000000001')\gexec

\echo ''
\echo 'Databases now present:'
SELECT datname AS database,
       pg_encoding_to_char(encoding) AS encoding,
       datcollate AS collation
FROM pg_database
WHERE datname IN ('retailerp_master', 'retailerp_design', 'retailerp_test', 'IN0000000001')
ORDER BY datname;
