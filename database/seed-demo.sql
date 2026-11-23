DO $$ BEGIN
 IF current_setting('vairas.demo',true) IS DISTINCT FROM 'on' THEN
   RAISE EXCEPTION 'Demo seed blocked: SET vairas.demo = on in an isolated demo database';
 END IF;
END $$;

 
 
DO $$
DECLARE n int; x text; sid uuid; op uuid; uid uuid; eid uuid; gid uuid; smid uuid; tmid uuid; amid uuid; iid uuid; iop uuid; rop uuid;
BEGIN
 FOR n IN 1..2 LOOP
  x=lpad(n::text,12,'0');sid=('10000000-0000-0000-0000-'||x)::uuid;
  uid=('30000000-0000-0000-0000-'||x)::uuid;eid=('50000000-0000-0000-0000-'||x)::uuid;
  gid=('91000000-0000-0000-0000-'||x)::uuid;smid=('81000000-0000-0000-0000-'||x)::uuid;
  tmid=('82000000-0000-0000-0000-'||x)::uuid;amid=('83000000-0000-0000-0000-'||x)::uuid;
  op=('90000000-0000-0000-0000-'||x)::uuid;iop=('95000000-0000-0000-0000-'||x)::uuid;
  rop=('96000000-0000-0000-0000-'||x)::uuid;iid=('97000000-0000-0000-0000-'||x)::uuid;
  INSERT INTO school(school_id,name,status,revision,created_at,updated_at)
   VALUES(sid,'Synthetic Demo School '||n,'Active',1,'2026-01-01T00:00:00Z','2026-01-01T00:00:00Z') ON CONFLICT DO NOTHING;
  INSERT INTO school_operation(operation_id,school_id,command_name,target_kind,target_id,actor_user_id,channel,status,request_fingerprint,requested_at,resolved_at,outcome_code,authority_basis)
   VALUES(op,sid,'LoadSyntheticRelations','School',sid,uid,'Internal','Committed',repeat('a',64),'2026-01-01T00:00:00Z','2026-01-01T00:00:00Z','DEMO_ONLY','isolated fixture loader, not a permission grant') ON CONFLICT DO NOTHING;
  INSERT INTO branch(branch_id,school_id,name,status,revision,last_operation_id)
   VALUES(('60000000-0000-0000-0000-'||x)::uuid,sid,'Synthetic Branch '||n,'Active',1,op) ON CONFLICT DO NOTHING;
  INSERT INTO school_membership(membership_id,school_id,user_id,role,started_at,revision,last_operation_id)
   VALUES(smid,sid,uid,'Student','2026-01-01T00:00:00Z',1,op),(tmid,sid,uid,'Teacher','2026-01-01T00:00:00Z',1,op),(amid,sid,uid,'Administrator','2026-01-01T00:00:00Z',1,op) ON CONFLICT DO NOTHING;
  INSERT INTO membership_permission_grant(grant_id,school_id,membership_id,permission,started_at,granted_operation_id)
   VALUES(('94000000-0000-0000-0000-'||x)::uuid,sid,tmid,'Assessor','2026-01-01T00:00:00Z',op) ON CONFLICT DO NOTHING;
  INSERT INTO student_enrollment(enrollment_id,school_id,student_id,category_code,started_at,status,status_changed_at,revision,last_operation_id)
   VALUES(eid,sid,('40000000-0000-0000-0000-'||x)::uuid,'DEMO','2026-01-01T00:00:00Z','Active','2026-01-01T00:00:00Z',1,op) ON CONFLICT DO NOTHING;
  INSERT INTO enrollment_status_history(enrollment_id,school_id,enrollment_revision,to_status,effective_at,recorded_at,operation_id)
   VALUES(eid,sid,1,'Active','2026-01-01T00:00:00Z','2026-01-01T00:00:00Z',op) ON CONFLICT DO NOTHING;
  INSERT INTO training_group(group_id,school_id,branch_id,name,category_code,started_at,revision,membership_revision,last_operation_id)
   VALUES(gid,sid,('60000000-0000-0000-0000-'||x)::uuid,'Synthetic Group '||n,'DEMO','2026-01-01T00:00:00Z',2,2,op) ON CONFLICT DO NOTHING;
  INSERT INTO student_group_membership(membership_id,school_id,enrollment_id,group_id,joined_at,association_program_version_id,association_verified_at,revision,last_operation_id)
   VALUES(('92000000-0000-0000-0000-'||x)::uuid,sid,eid,gid,'2026-01-02T00:00:00Z',('51000000-0000-0000-0000-'||x)::uuid,'2026-01-02T00:00:00Z',1,op) ON CONFLICT DO NOTHING;
   
   
  INSERT INTO teacher_group_assignment(assignment_id,school_id,teacher_id,group_id,assigned_at,revision,last_operation_id)
   VALUES(('93000000-0000-0000-0000-'||x)::uuid,sid,('70000000-0000-0000-0000-'||x)::uuid,gid,'2026-01-01T00:00:00Z',1,op) ON CONFLICT DO NOTHING;
  INSERT INTO group_roster_revision(group_id,school_id,membership_revision,recorded_at,operation_id)
   VALUES(gid,sid,1,'2026-01-01T00:00:00Z',op),(gid,sid,2,'2026-01-02T00:00:00Z',op) ON CONFLICT DO NOTHING;
  INSERT INTO group_roster_member(group_id,school_id,membership_revision,membership_id,enrollment_id,student_id,enrollment_revision,enrollment_status,category_code,enrollment_started_at,joined_at)
   VALUES(gid,sid,2,('92000000-0000-0000-0000-'||x)::uuid,eid,('40000000-0000-0000-0000-'||x)::uuid,1,'Active','DEMO','2026-01-01T00:00:00Z','2026-01-02T00:00:00Z') ON CONFLICT DO NOTHING;
  INSERT INTO school_operation(operation_id,school_id,command_name,target_kind,target_id,actor_user_id,channel,status,request_fingerprint,requested_at,resolved_at,outcome_code,authority_basis)
   VALUES(iop,sid,'IssueSyntheticInvitation','SchoolInvitation',iid,uid,'Internal','Committed',repeat('b',64),'2026-01-03T00:00:00Z','2026-01-03T00:00:00Z','DEMO_ONLY','synthetic current school Administrator') ON CONFLICT DO NOTHING;
  IF n=2 THEN
   INSERT INTO school_operation(operation_id,school_id,command_name,target_kind,target_id,actor_user_id,channel,status,request_fingerprint,requested_at,resolved_at,outcome_code,authority_basis)
    VALUES(rop,sid,'RevokeSyntheticInvitation','SchoolInvitation',iid,uid,'Internal','Committed',repeat('c',64),'2026-01-03T12:00Z','2026-01-03T12:00Z','DEMO_ONLY','synthetic invitation issuer') ON CONFLICT DO NOTHING;
  END IF;
  INSERT INTO school_invitation(invitation_id,school_id,target_user_id,role,category_code,issuer_membership_id,status,revision,created_at,expires_at,issued_operation_id,revoked_operation_id,revoked_at)
   VALUES(iid,sid,uid,'Student','DEMO',amid,CASE n WHEN 1 THEN 'Pending'::sm_invitation_status ELSE 'Revoked'::sm_invitation_status END,CASE n WHEN 1 THEN 1 ELSE 2 END,'2026-01-03T00:00:00Z','2026-01-04T00:00:00Z',iop,CASE n WHEN 2 THEN rop ELSE NULL END,CASE n WHEN 2 THEN '2026-01-03T12:00Z'::timestamptz ELSE NULL END) ON CONFLICT DO NOTHING;
 END LOOP;
END $$;
