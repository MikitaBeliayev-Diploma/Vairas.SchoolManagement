-- Run as migration owner: psql -v school_id=<UUID> -v school_name=<name> -f ...
-- Creates only the tenant; it grants no memberships or bypass of activation.
INSERT INTO school(school_id,name,status,revision,created_at,updated_at)
VALUES (:'school_id'::uuid,:'school_name','Active',1,clock_timestamp(),clock_timestamp());
