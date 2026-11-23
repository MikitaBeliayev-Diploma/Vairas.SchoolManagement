CREATE TYPE "sm_status" AS ENUM (
  'Active',
  'Inactive'
);

CREATE TYPE "sm_role" AS ENUM (
  'Student',
  'Teacher',
  'Administrator'
);

CREATE TYPE "sm_permission" AS ENUM (
  'Assessor',
  'SchoolRuleOwner'
);

CREATE TYPE "sm_enrollment_status" AS ENUM (
  'Active',
  'Completed',
  'Withdrawn',
  'Transferred',
  'Cancelled'
);

CREATE TYPE "sm_operation_status" AS ENUM (
  'Pending',
  'Committed',
  'Rejected',
  'ReconciliationPending'
);

CREATE TYPE "sm_channel" AS ENUM (
  'Web',
  'MCP',
  'Internal'
);

CREATE TYPE "sm_invitation_status" AS ENUM (
  'Pending',
  'Accepted',
  'Revoked'
);

CREATE TABLE "school" (
  "school_id" uuid PRIMARY KEY NOT NULL,
  "name" varchar(200) NOT NULL,
  "status" sm_status NOT NULL DEFAULT 'Active',
  "revision" bigint NOT NULL DEFAULT 1,
  "created_at" timestamptz NOT NULL,
  "updated_at" timestamptz NOT NULL,
  CONSTRAINT "school_name_nonempty" CHECK (length(trim(name)) > 0),
  CONSTRAINT "school_revision_time" CHECK (revision > 0 AND updated_at >= created_at)
);

CREATE TABLE "school_operation" (
  "operation_id" uuid NOT NULL,
  "school_id" uuid NOT NULL,
  "command_name" varchar(80) NOT NULL,
  "target_kind" varchar(60) NOT NULL,
  "target_id" uuid NOT NULL,
  "expected_revision" bigint,
  "resulting_revision" bigint,
  "coordination_required" boolean NOT NULL DEFAULT false,
  "expected_school_revision" bigint,
  "resulting_school_revision" bigint,
  "actor_user_id" uuid NOT NULL,
  "channel" sm_channel NOT NULL,
  "status" sm_operation_status NOT NULL,
  "request_fingerprint" varchar(64) NOT NULL,
  "requested_at" timestamptz NOT NULL,
  "resolved_at" timestamptz,
  "reason" text,
  "outcome_code" varchar(80),
  "authority_basis" varchar(200) NOT NULL,
  "confirmation_id" uuid,
  "confirmed_at" timestamptz,
  CONSTRAINT "operation_expected_revision" CHECK (expected_revision IS NULL OR expected_revision > 0),
  CONSTRAINT "operation_result_revision" CHECK (resulting_revision IS NULL OR resulting_revision > 0),
  CONSTRAINT "operation_coordination_revisions" CHECK ((coordination_required = false AND expected_school_revision IS NULL AND resulting_school_revision IS NULL) OR (coordination_required = true AND expected_school_revision IS NOT NULL AND expected_school_revision > 0 AND (resulting_school_revision IS NULL OR resulting_school_revision = expected_school_revision + 1))),
  CONSTRAINT "operation_coordinated_commit" CHECK (status <> 'Committed' OR coordination_required = false OR resulting_school_revision IS NOT NULL),
  CONSTRAINT "operation_confirmation_pair" CHECK ((confirmation_id IS NULL) = (confirmed_at IS NULL)),
  CONSTRAINT "operation_mcp_confirmation" CHECK (status <> 'Committed' OR channel <> 'MCP' OR confirmation_id IS NOT NULL),
  CONSTRAINT "operation_resolution_shape" CHECK ((status IN ('Committed','Rejected') AND resolved_at IS NOT NULL) OR (status IN ('Pending','ReconciliationPending') AND resolved_at IS NULL)),
  CONSTRAINT "operation_resolution_time" CHECK (resolved_at IS NULL OR resolved_at >= requested_at),
  CONSTRAINT "operation_required_metadata" CHECK (length(trim(command_name)) > 0 AND length(trim(authority_basis)) > 0 AND length(request_fingerprint) = 64),
  PRIMARY KEY ("operation_id", "school_id")
);

CREATE TABLE "branch" (
  "branch_id" uuid NOT NULL,
  "school_id" uuid NOT NULL,
  "name" varchar(200) NOT NULL,
  "address_line" varchar(300),
  "city" varchar(120),
  "postal_code" varchar(30),
  "country_code" varchar(2),
  "status" sm_status NOT NULL DEFAULT 'Active',
  "revision" bigint NOT NULL DEFAULT 1,
  "last_operation_id" uuid NOT NULL,
  CONSTRAINT "branch_name_revision" CHECK (length(trim(name)) > 0 AND revision > 0),
  PRIMARY KEY ("branch_id", "school_id")
);

CREATE TABLE "school_membership" (
  "membership_id" uuid NOT NULL,
  "school_id" uuid NOT NULL,
  "user_id" uuid NOT NULL,
  "role" sm_role NOT NULL,
  "started_at" timestamptz NOT NULL,
  "ended_at" timestamptz,
  "revision" bigint NOT NULL DEFAULT 1,
  "last_operation_id" uuid NOT NULL,
  CONSTRAINT "membership_period" CHECK (ended_at IS NULL OR ended_at > started_at),
  CONSTRAINT "membership_revision" CHECK (revision > 0),
  PRIMARY KEY ("membership_id", "school_id")
);

CREATE TABLE "membership_permission_grant" (
  "grant_id" uuid NOT NULL,
  "school_id" uuid NOT NULL,
  "membership_id" uuid NOT NULL,
  "permission" sm_permission NOT NULL,
  "started_at" timestamptz NOT NULL,
  "ended_at" timestamptz,
  "granted_operation_id" uuid NOT NULL,
  "ended_operation_id" uuid,
  CONSTRAINT "grant_period" CHECK (ended_at IS NULL OR ended_at > started_at),
  CONSTRAINT "grant_end_evidence" CHECK ((ended_at IS NULL) = (ended_operation_id IS NULL)),
  PRIMARY KEY ("grant_id", "school_id")
);

CREATE TABLE "student_enrollment" (
  "enrollment_id" uuid NOT NULL,
  "school_id" uuid NOT NULL,
  "student_id" uuid NOT NULL,
  "category_code" varchar(20) NOT NULL,
  "started_at" timestamptz NOT NULL,
  "ended_at" timestamptz,
  "status" sm_enrollment_status NOT NULL DEFAULT 'Active',
  "status_changed_at" timestamptz NOT NULL DEFAULT (clock_timestamp()),
  "revision" bigint NOT NULL DEFAULT 1,
  "last_operation_id" uuid NOT NULL,
  CONSTRAINT "enrollment_category_revision" CHECK (length(trim(category_code)) > 0 AND revision > 0),
  CONSTRAINT "enrollment_period" CHECK (ended_at IS NULL OR ended_at > started_at),
  CONSTRAINT "enrollment_finite_lifecycle" CHECK (isfinite(started_at) AND isfinite(status_changed_at) AND (ended_at IS NULL OR isfinite(ended_at))),
  CONSTRAINT "enrollment_lifecycle_shape" CHECK ((status = 'Active' AND ended_at IS NULL) OR (status <> 'Active' AND ended_at IS NOT NULL AND ended_at = status_changed_at)),
  PRIMARY KEY ("enrollment_id", "school_id")
);

CREATE TABLE "enrollment_status_history" (
  "enrollment_id" uuid NOT NULL,
  "school_id" uuid NOT NULL,
  "enrollment_revision" bigint NOT NULL,
  "from_status" sm_enrollment_status,
  "to_status" sm_enrollment_status NOT NULL,
  "effective_at" timestamptz NOT NULL,
  "recorded_at" timestamptz NOT NULL DEFAULT (clock_timestamp()),
  "operation_id" uuid NOT NULL,
  "completion_evidence_id" uuid,
  "completion_dependency_revision" bigint,
  "completion_verified_at" timestamptz,
  CONSTRAINT "enrollment_history_revision" CHECK (enrollment_revision > 0),
  CONSTRAINT "enrollment_history_immediate_terminal" CHECK (isfinite(effective_at) AND isfinite(recorded_at) AND (to_status = 'Active' OR effective_at = recorded_at)),
  CONSTRAINT "enrollment_history_transition" CHECK ((from_status IS NULL AND to_status = 'Active') OR (from_status IS NOT NULL AND from_status = 'Active' AND to_status <> 'Active')),
  CONSTRAINT "completion_basis_required" CHECK (to_status <> 'Completed' OR (completion_evidence_id IS NOT NULL AND completion_dependency_revision IS NOT NULL AND completion_dependency_revision > 0 AND completion_verified_at IS NOT NULL)),
  PRIMARY KEY ("enrollment_id", "school_id", "enrollment_revision")
);

CREATE TABLE "training_group" (
  "group_id" uuid NOT NULL,
  "school_id" uuid NOT NULL,
  "branch_id" uuid,
  "name" varchar(200) NOT NULL,
  "category_code" varchar(20),
  "started_at" timestamptz NOT NULL,
  "ended_at" timestamptz,
  "revision" bigint NOT NULL DEFAULT 1,
  "membership_revision" bigint NOT NULL DEFAULT 1,
  "last_operation_id" uuid NOT NULL,
  CONSTRAINT "group_name_revisions" CHECK (length(trim(name)) > 0 AND revision > 0 AND membership_revision > 0),
  CONSTRAINT "group_period" CHECK (ended_at IS NULL OR ended_at > started_at),
  CONSTRAINT "group_optional_category" CHECK (category_code IS NULL OR length(trim(category_code)) > 0),
  PRIMARY KEY ("group_id", "school_id")
);

CREATE TABLE "student_group_membership" (
  "membership_id" uuid NOT NULL,
  "school_id" uuid NOT NULL,
  "enrollment_id" uuid NOT NULL,
  "group_id" uuid NOT NULL,
  "joined_at" timestamptz NOT NULL,
  "left_at" timestamptz,
  "association_program_version_id" uuid NOT NULL,
  "association_verified_at" timestamptz NOT NULL,
  "revision" bigint NOT NULL DEFAULT 1,
  "last_operation_id" uuid NOT NULL,
  CONSTRAINT "student_group_period" CHECK (left_at IS NULL OR left_at > joined_at),
  CONSTRAINT "association_revision" CHECK (revision > 0),
  PRIMARY KEY ("membership_id", "school_id")
);

CREATE TABLE "teacher_group_assignment" (
  "assignment_id" uuid NOT NULL,
  "school_id" uuid NOT NULL,
  "teacher_id" uuid NOT NULL,
  "group_id" uuid NOT NULL,
  "assigned_at" timestamptz NOT NULL,
  "ended_at" timestamptz,
  "revision" bigint NOT NULL DEFAULT 1,
  "last_operation_id" uuid NOT NULL,
  CONSTRAINT "teacher_assignment_period" CHECK (ended_at IS NULL OR ended_at > assigned_at),
  CONSTRAINT "teacher_assignment_revision" CHECK (revision > 0),
  PRIMARY KEY ("assignment_id", "school_id")
);

CREATE TABLE "group_roster_revision" (
  "group_id" uuid NOT NULL,
  "school_id" uuid NOT NULL,
  "membership_revision" bigint NOT NULL,
  "recorded_at" timestamptz NOT NULL,
  "operation_id" uuid NOT NULL,
  CONSTRAINT "roster_revision_positive" CHECK (membership_revision > 0),
  PRIMARY KEY ("group_id", "school_id", "membership_revision")
);

CREATE TABLE "group_roster_member" (
  "group_id" uuid NOT NULL,
  "school_id" uuid NOT NULL,
  "membership_revision" bigint NOT NULL,
  "membership_id" uuid NOT NULL,
  "enrollment_id" uuid NOT NULL,
  "student_id" uuid NOT NULL,
  "enrollment_revision" bigint NOT NULL,
  "enrollment_status" sm_enrollment_status NOT NULL,
  "category_code" varchar(20) NOT NULL,
  "enrollment_started_at" timestamptz NOT NULL,
  "enrollment_ended_at" timestamptz,
  "joined_at" timestamptz NOT NULL,
  "left_at" timestamptz,
  CONSTRAINT "roster_enrollment_basis" CHECK (enrollment_revision > 0 AND length(trim(category_code)) > 0),
  CONSTRAINT "roster_membership_period" CHECK (left_at IS NULL OR left_at > joined_at),
  CONSTRAINT "roster_enrollment_period" CHECK (enrollment_ended_at IS NULL OR enrollment_ended_at > enrollment_started_at),
  CONSTRAINT "roster_enrollment_shape" CHECK ((enrollment_status = 'Active' AND enrollment_ended_at IS NULL) OR (enrollment_status <> 'Active' AND enrollment_ended_at IS NOT NULL)),
  PRIMARY KEY ("group_id", "school_id", "membership_revision", "membership_id")
);

CREATE TABLE "school_invitation" (
  "invitation_id" uuid NOT NULL,
  "school_id" uuid NOT NULL,
  "target_user_id" uuid NOT NULL,
  "role" sm_role NOT NULL,
  "category_code" varchar(20),
  "issuer_membership_id" uuid,
  "status" sm_invitation_status NOT NULL DEFAULT 'Pending',
  "revision" bigint NOT NULL DEFAULT 1,
  "created_at" timestamptz NOT NULL,
  "expires_at" timestamptz NOT NULL,
  "issued_operation_id" uuid NOT NULL,
  "accepted_operation_id" uuid,
  "accepted_at" timestamptz,
  "result_membership_id" uuid,
  "result_enrollment_id" uuid,
  "revoked_operation_id" uuid,
  "revoked_at" timestamptz,
  CONSTRAINT "invitation_revision_expiry" CHECK (revision > 0 AND isfinite(created_at) AND isfinite(expires_at) AND expires_at > created_at),
  CONSTRAINT "invitation_category_shape" CHECK ((role = 'Student' AND category_code IS NOT NULL AND length(trim(category_code)) > 0) OR (role <> 'Student' AND category_code IS NULL)),
  CONSTRAINT "invitation_admin_platform_basis" CHECK (role <> 'Administrator' OR issuer_membership_id IS NULL),
  CONSTRAINT "invitation_state_shape" CHECK ((status = 'Pending' AND accepted_operation_id IS NULL AND accepted_at IS NULL AND result_membership_id IS NULL AND result_enrollment_id IS NULL AND revoked_operation_id IS NULL AND revoked_at IS NULL) OR (status = 'Accepted' AND accepted_operation_id IS NOT NULL AND accepted_at IS NOT NULL AND result_membership_id IS NOT NULL AND revoked_operation_id IS NULL AND revoked_at IS NULL AND ((role = 'Student' AND result_enrollment_id IS NOT NULL) OR (role <> 'Student' AND result_enrollment_id IS NULL))) OR (status = 'Revoked' AND revoked_operation_id IS NOT NULL AND revoked_at IS NOT NULL AND accepted_operation_id IS NULL AND accepted_at IS NULL AND result_membership_id IS NULL AND result_enrollment_id IS NULL)),
  CONSTRAINT "invitation_accept_time" CHECK (accepted_at IS NULL OR (isfinite(accepted_at) AND accepted_at >= created_at AND accepted_at < expires_at)),
  CONSTRAINT "invitation_revoke_time" CHECK (revoked_at IS NULL OR (isfinite(revoked_at) AND revoked_at >= created_at)),
  PRIMARY KEY ("invitation_id", "school_id")
);

CREATE INDEX ON "school_operation" ("school_id", "target_kind", "target_id", "requested_at");

CREATE INDEX ON "school_membership" ("school_id", "user_id", "role", "started_at");

CREATE INDEX ON "membership_permission_grant" ("membership_id", "school_id", "permission", "started_at");

CREATE UNIQUE INDEX "sm_enrollment_global_identity" ON "student_enrollment" ("enrollment_id");

CREATE INDEX ON "student_enrollment" ("school_id", "student_id", "status");

CREATE INDEX ON "student_group_membership" ("school_id", "group_id", "joined_at");

CREATE INDEX ON "student_group_membership" ("school_id", "enrollment_id", "joined_at");

CREATE UNIQUE INDEX ON "student_group_membership" ("membership_id", "school_id", "group_id", "enrollment_id");

CREATE INDEX ON "teacher_group_assignment" ("school_id", "group_id", "assigned_at");

CREATE INDEX ON "teacher_group_assignment" ("school_id", "teacher_id", "assigned_at");

CREATE UNIQUE INDEX ON "school_invitation" ("issued_operation_id", "school_id");

CREATE UNIQUE INDEX ON "school_invitation" ("accepted_operation_id", "school_id");

CREATE UNIQUE INDEX ON "school_invitation" ("revoked_operation_id", "school_id");

CREATE INDEX ON "school_invitation" ("school_id", "target_user_id", "status", "expires_at");





















































ALTER TABLE "school_invitation" ADD FOREIGN KEY ("school_id") REFERENCES "school" ("school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "school_invitation" ADD FOREIGN KEY ("issuer_membership_id", "school_id") REFERENCES "school_membership" ("membership_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "school_invitation" ADD FOREIGN KEY ("issued_operation_id", "school_id") REFERENCES "school_operation" ("operation_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "school_invitation" ADD FOREIGN KEY ("accepted_operation_id", "school_id") REFERENCES "school_operation" ("operation_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "school_invitation" ADD FOREIGN KEY ("revoked_operation_id", "school_id") REFERENCES "school_operation" ("operation_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "school_invitation" ADD FOREIGN KEY ("result_membership_id", "school_id") REFERENCES "school_membership" ("membership_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "school_invitation" ADD FOREIGN KEY ("result_enrollment_id", "school_id") REFERENCES "student_enrollment" ("enrollment_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "school_operation" ADD FOREIGN KEY ("school_id") REFERENCES "school" ("school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "branch" ADD FOREIGN KEY ("school_id") REFERENCES "school" ("school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "school_membership" ADD FOREIGN KEY ("school_id") REFERENCES "school" ("school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "student_enrollment" ADD FOREIGN KEY ("school_id") REFERENCES "school" ("school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "training_group" ADD FOREIGN KEY ("school_id") REFERENCES "school" ("school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "training_group" ADD FOREIGN KEY ("branch_id", "school_id") REFERENCES "branch" ("branch_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "branch" ADD FOREIGN KEY ("last_operation_id", "school_id") REFERENCES "school_operation" ("operation_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "school_membership" ADD FOREIGN KEY ("last_operation_id", "school_id") REFERENCES "school_operation" ("operation_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "student_enrollment" ADD FOREIGN KEY ("last_operation_id", "school_id") REFERENCES "school_operation" ("operation_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "training_group" ADD FOREIGN KEY ("last_operation_id", "school_id") REFERENCES "school_operation" ("operation_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "membership_permission_grant" ADD FOREIGN KEY ("membership_id", "school_id") REFERENCES "school_membership" ("membership_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "membership_permission_grant" ADD FOREIGN KEY ("granted_operation_id", "school_id") REFERENCES "school_operation" ("operation_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "membership_permission_grant" ADD FOREIGN KEY ("ended_operation_id", "school_id") REFERENCES "school_operation" ("operation_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "enrollment_status_history" ADD FOREIGN KEY ("enrollment_id", "school_id") REFERENCES "student_enrollment" ("enrollment_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "enrollment_status_history" ADD FOREIGN KEY ("operation_id", "school_id") REFERENCES "school_operation" ("operation_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "student_group_membership" ADD FOREIGN KEY ("enrollment_id", "school_id") REFERENCES "student_enrollment" ("enrollment_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "student_group_membership" ADD FOREIGN KEY ("group_id", "school_id") REFERENCES "training_group" ("group_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "student_group_membership" ADD FOREIGN KEY ("last_operation_id", "school_id") REFERENCES "school_operation" ("operation_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "teacher_group_assignment" ADD FOREIGN KEY ("group_id", "school_id") REFERENCES "training_group" ("group_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "teacher_group_assignment" ADD FOREIGN KEY ("last_operation_id", "school_id") REFERENCES "school_operation" ("operation_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "group_roster_revision" ADD FOREIGN KEY ("group_id", "school_id") REFERENCES "training_group" ("group_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "group_roster_revision" ADD FOREIGN KEY ("operation_id", "school_id") REFERENCES "school_operation" ("operation_id", "school_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "group_roster_member" ADD FOREIGN KEY ("group_id", "school_id", "membership_revision") REFERENCES "group_roster_revision" ("group_id", "school_id", "membership_revision") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "group_roster_member" ADD FOREIGN KEY ("membership_id", "school_id", "group_id", "enrollment_id") REFERENCES "student_group_membership" ("membership_id", "school_id", "group_id", "enrollment_id") ON DELETE RESTRICT DEFERRABLE INITIALLY IMMEDIATE;
