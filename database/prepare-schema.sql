-- Run as database administrator against the new School database, before EF migrations.
-- Explicit ownership is also needed on older PostgreSQL installations.
ALTER SCHEMA public OWNER TO v_school_management_migration;
REVOKE ALL ON SCHEMA public FROM PUBLIC;
GRANT USAGE,CREATE ON SCHEMA public TO v_school_management_migration;
