-- The master database and the first tenant shard (D-02, TK-27).
--
-- Postgres runs this once, on the container's first start with an empty data
-- volume; an existing install already has both databases and never sees it.
-- Master migrates them on its own start but no longer creates them outside
-- Development, so a missing one stops Master with a message naming this file.
--
-- UTF8 from template0, so Tamil and Chinese names store as characters.
CREATE DATABASE "EP_Admin" ENCODING 'UTF8' TEMPLATE template0;
CREATE DATABASE "IN000001" ENCODING 'UTF8' TEMPLATE template0;
