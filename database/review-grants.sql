-- Deactivation reviews (migration DeactivationReview). Apply as migration owner after runtime-grants.sql; re-runnable.
GRANT SELECT,INSERT ON membership_review,integration_inbox TO v_school_management_runtime;
GRANT UPDATE(resolved_at,resolved_by_user_id,resolution_note) ON membership_review TO v_school_management_runtime;
ALTER TABLE membership_review ENABLE ROW LEVEL SECURITY;
DO $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM pg_policies WHERE schemaname='public' AND tablename='membership_review' AND policyname='tenant_scope') THEN
  CREATE POLICY tenant_scope ON membership_review TO v_school_management_runtime
   USING (school_id=nullif(current_setting('vairas.school_id',true),'')::uuid) WITH CHECK (school_id=nullif(current_setting('vairas.school_id',true),'')::uuid);
 END IF;
END $$;
-- integration_inbox is global (an event can affect several schools) and holds identifiers only; no RLS by design.
REVOKE UPDATE,DELETE,TRUNCATE ON integration_inbox FROM v_school_management_runtime;
REVOKE DELETE,TRUNCATE ON membership_review FROM v_school_management_runtime;
