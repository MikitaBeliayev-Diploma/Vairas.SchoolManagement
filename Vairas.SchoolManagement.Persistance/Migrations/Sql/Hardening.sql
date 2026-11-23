-- Narrow trusted-service discovery. The HTTP layer supplies only freshly resolved self IDs.
CREATE FUNCTION public.school_self_discovery(p_user uuid,p_student uuid)
RETURNS TABLE(school_id uuid) LANGUAGE sql STABLE SECURITY DEFINER
SET search_path=pg_catalog,public,pg_temp AS $$
 SELECT m.school_id FROM public.school_membership m WHERE m.user_id=p_user
 UNION SELECT i.school_id FROM public.school_invitation i WHERE i.target_user_id=p_user
 UNION SELECT e.school_id FROM public.student_enrollment e WHERE p_student IS NOT NULL AND e.student_id=p_student;
$$;
REVOKE ALL ON FUNCTION public.school_self_discovery(uuid,uuid) FROM PUBLIC;

CREATE FUNCTION public.school_evidence_immutable() RETURNS trigger LANGUAGE plpgsql
SET search_path=pg_catalog,public,pg_temp AS $$ BEGIN
 RAISE EXCEPTION 'School evidence is append-only' USING ERRCODE='23514';
END $$;
CREATE TRIGGER sm_history_immutable BEFORE UPDATE OR DELETE ON enrollment_status_history FOR EACH ROW EXECUTE FUNCTION public.school_evidence_immutable();
CREATE TRIGGER sm_roster_revision_immutable BEFORE UPDATE OR DELETE ON group_roster_revision FOR EACH ROW EXECUTE FUNCTION public.school_evidence_immutable();
CREATE TRIGGER sm_roster_member_immutable BEFORE UPDATE OR DELETE ON group_roster_member FOR EACH ROW EXECUTE FUNCTION public.school_evidence_immutable();

CREATE FUNCTION public.school_invitation_guard() RETURNS trigger LANGUAGE plpgsql
SET search_path=pg_catalog,public,pg_temp AS $$ BEGIN
 IF (to_jsonb(NEW)-ARRAY['status','revision','accepted_operation_id','accepted_at','result_membership_id','result_enrollment_id','revoked_operation_id','revoked_at'])
  IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['status','revision','accepted_operation_id','accepted_at','result_membership_id','result_enrollment_id','revoked_operation_id','revoked_at'])
  OR OLD.status <> 'Pending' OR NEW.status NOT IN ('Accepted','Revoked') OR NEW.revision <> OLD.revision+1 THEN
  RAISE EXCEPTION 'Invalid invitation transition or immutable binding change' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER sm_invitation_guard BEFORE UPDATE ON school_invitation FOR EACH ROW EXECUTE FUNCTION public.school_invitation_guard();

CREATE FUNCTION public.school_operation_guard() RETURNS trigger LANGUAGE plpgsql
SET search_path=pg_catalog,public,pg_temp AS $$ BEGIN
 IF OLD.status IN ('Committed','Rejected') OR
 (to_jsonb(NEW)-ARRAY['status','resolved_at','resulting_revision','resulting_school_revision','outcome_code'])
 IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['status','resolved_at','resulting_revision','resulting_school_revision','outcome_code']) THEN
  RAISE EXCEPTION 'Immutable school operation' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER sm_operation_guard BEFORE UPDATE ON school_operation FOR EACH ROW EXECUTE FUNCTION public.school_operation_guard();
