\set ON_ERROR_STOP on
DO $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='v_school_management_delivery') THEN
  CREATE ROLE v_school_management_delivery LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS NOREPLICATION;
 END IF;
END $$;
GRANT USAGE ON SCHEMA public TO v_school_management_delivery;
DO $$ BEGIN EXECUTE format('GRANT CONNECT ON DATABASE %I TO v_school_management_delivery',current_database()); END $$;
GRANT SELECT,UPDATE ON school_learning_outbox TO v_school_management_delivery;
CREATE POLICY learning_delivery ON school_learning_outbox TO v_school_management_delivery USING(true) WITH CHECK(true);
