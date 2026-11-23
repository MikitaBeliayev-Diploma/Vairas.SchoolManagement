CREATE EXTENSION IF NOT EXISTS btree_gist;

ALTER TABLE school_membership
  ADD CONSTRAINT sm_membership_finite CHECK
    (isfinite(started_at) AND (ended_at IS NULL OR isfinite(ended_at))),
  ADD CONSTRAINT sm_membership_no_overlap EXCLUDE USING gist
    (school_id WITH =, user_id WITH =, role WITH =,
     tstzrange(started_at, ended_at, '[)') WITH &&);

ALTER TABLE membership_permission_grant
  ADD CONSTRAINT sm_grant_finite CHECK
    (isfinite(started_at) AND (ended_at IS NULL OR isfinite(ended_at))),
  ADD CONSTRAINT sm_grant_no_overlap EXCLUDE USING gist
    (school_id WITH =, membership_id WITH =, permission WITH =,
     tstzrange(started_at, ended_at, '[)') WITH &&);

ALTER TABLE student_group_membership
  ADD CONSTRAINT sm_student_group_finite CHECK
    (isfinite(joined_at) AND (left_at IS NULL OR isfinite(left_at))),
  ADD CONSTRAINT sm_student_group_no_overlap EXCLUDE USING gist
    (school_id WITH =, enrollment_id WITH =, group_id WITH =,
     tstzrange(joined_at, left_at, '[)') WITH &&);

ALTER TABLE teacher_group_assignment
  ADD CONSTRAINT sm_teacher_group_finite CHECK
    (isfinite(assigned_at) AND (ended_at IS NULL OR isfinite(ended_at))),
  ADD CONSTRAINT sm_teacher_group_no_overlap EXCLUDE USING gist
    (school_id WITH =, teacher_id WITH =, group_id WITH =,
     tstzrange(assigned_at, ended_at, '[)') WITH &&);

 
 
 
CREATE UNIQUE INDEX sm_one_unresolved_coordination_per_school
  ON school_operation (school_id)
  WHERE coordination_required
    AND status IN ('Pending', 'ReconciliationPending');
