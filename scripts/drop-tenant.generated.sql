ALTER DATABASE "IN0000000001" WITH ALLOW_CONNECTIONS false;
SELECT pg_terminate_backend(pid) FROM pg_stat_activity
 WHERE datname = 'IN0000000001' AND pid <> pg_backend_pid();
DROP DATABASE "IN0000000001";
