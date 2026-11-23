# Vairas School Management

ASP.NET Core 10 service for school organization, target-bound invitations, school-local roles, enrollments and immutable roster evidence. PostgreSQL and EF Core own the service's persistence. Authentication owns credentials/sessions; User Management owns global UserId and role-profile bindings; Learning Tracking owns programmes, credit and readiness.

**Repository:** https://github.com/MikitaBeliayev-Diploma/Vairas.SchoolManagement (private). Grant the reviewer read access before submission. The repository starts with the actual initial import; earlier implementation work is not represented as invented historical commits. Source code, migrations, API, tests and verification tooling are included. Docker, CI and production deployment remain outside this stage; there is no public production URL.

## Implemented behavior and integration boundary

- Read school/branch metadata and authorized groups, memberships and enrollments.
- Create/update branch address metadata, rename schools/groups, create empty groups with an immutable empty roster revision.
- Issue a persisted 24-hour invitation to an exact UserId, list/read one's own invitations, revoke Pending invitations, validate staff-profile provisioning for the existing User Management API.
- Accept a staff invitation locally **only when it reuses an unchanged effective membership**. Exact retries return the original acceptance result. New student enrollment or new effective membership requires the Scheduling eligibility gate.
- Read exact retained roster revisions for a complete interval. An unknown historical revision is never replaced by the current roster. Duplicate canonical students within one returned roster are rejected.
- Compute current enrollment/teacher eligibility from local scope and authoritative UM bindings/activity. This read is not a reservation or a reusable permit.

**OPEN-GATE-01:** Scheduling's durable eligibility barrier/revision floor/recovery protocol is not implemented in the current Backend. Enrollment creation/termination, new membership activation (including the first Administrator), group association, teacher assignment and school/branch eligibility changes therefore return **503 ACTIVATION_INTEGRATION_NOT_READY before business writes**. No fake 200/202, local-calendar shortcut or automatic activation is provided. The direct enrollment/group-assignment routes are exposed for contract continuity but remain unavailable.

Programme applicability and completion evidence have no production provider here. Planning inputs report these dependencies as missing. Assessor/SchoolRuleOwner grant-management policy remains unspecified, so this release provides no grant-write API. There is no School broker subscriber/publisher or MCP write surface; current activity checks remain authoritative.

## Architecture and criteria

| Criterion | Implementation / evidence |
|---|---|
| Modern framework, DB, ORM | ASP.NET Core 10, PostgreSQL, EF Core / Npgsql 10 |
| Layered architecture / SOLID | API controllers depend on ISchoolService; Business depends on Domain and ISchoolStore/IUserActivity ports; Persistance implements transactional data access |
| API description | Development /openapi/v1.json; versioned [OpenAPI export](api/school-management.openapi.json); [HTTP examples](Vairas.SchoolManagement/Vairas.SchoolManagement.http) |
| Global errors | Middleware/ApiErrors.cs, ProblemDetails with stable code and correlationId |
| Structured logging | JSON console, request timing, safe failure codes, X-Correlation-ID propagation; optional OpenTelemetry OTLP |
| Unit coverage >=70% | scripts/verify.ps1 enforces **Business line coverage from unit tests only**, using Coverlet Cobertura |
| PostgreSQL integrity/isolation | Thirteen canonical tables, native enum mappings, composite FKs, GiST exclusions, RLS, immutable evidence/operation guards |
| Git repository / history | Private GitHub repository above, main branch; reviewer needs read access |
| Production externally accessible | Not deployed in this stage; local/in-process tests do not satisfy this criterion |

Projects: Vairas.SchoolManagement (presentation/adapters), .Business, .Domain, .Persistance, .Tests. The existing Backend spelling Persistance is retained. No paid libraries, MediatR, AutoMapper or runtime fake providers.

## Prerequisites

Run commands from this solution directory. Install .NET SDK 10, a supported PostgreSQL server with btree_gist available, and psql for the provisioning scripts. Development uses an HTTPS certificate trusted by both the browser and service clients:

```powershell
dotnet dev-certs https --trust
dotnet restore Vairas.SchoolManagement.slnx
dotnet tool restore
dotnet build Vairas.SchoolManagement.slnx -c Release
```

A standalone clone builds the service and all unit tests without sibling repositories. Cross-service integration test sources are included but compile only when both sibling Authentication and User Management projects exist (or EnableServiceIntegrationTests=true is explicitly supplied). No source changes to those services are required by School.

## Initialize a new database

Use a database administrator to run database/bootstrap-roles.sql. Set each role's password outside Git (for example with psql's interactive password command), and create an empty database owned by v_school_management_migration:

```sql
CREATE DATABASE vairas_school_management OWNER v_school_management_migration;
```

As database administrator, run database/prepare-schema.sql against this new application database before migrating. It assigns public schema ownership to the migration role and removes PUBLIC creation rights; database ownership alone is insufficient on older installations.

Supply the migration-owner connection externally, then apply EF migrations and runtime grants:

```powershell
$env:VAIRAS_MIGRATION_CONNECTION = '<migration-owner Npgsql connection>'
dotnet ef database update --project Vairas.SchoolManagement.Persistance --startup-project Vairas.SchoolManagement.Persistance
psql '<migration-owner PostgreSQL URI>' -v ON_ERROR_STOP=1 -f database/runtime-grants.sql
```

Npgsql connection strings and psql connection URIs are different formats; provide each externally. Do not run the original initial DDL after EF migration. Initial.sql is a byte-for-byte frozen canonical source; the companion exclusions and implementation hardening execute in the EF migration's transaction. Migrations are never automatically applied on application startup. Automatic downgrade is refused to protect retained evidence.

Runtime connects as v_school_management_runtime, never the migration owner. RLS is enabled on all thirteen domain tables. The service sets transaction-local school scope; no scope survives commit/rollback. Runtime has no DELETE, ownership or RLS bypass. The SECURITY DEFINER self-discovery function returns only school IDs associated with server-resolved self UserId/StudentId; HTTP APIs never accept discovery actor IDs from the caller. This is a trusted-service mechanism, not direct end-user database access.

Provision an initial school through database/provision-school.sql as migration owner, passing psql variables school_id and school_name. It creates only the tenant and grants no membership. Configure an existing active UM UserId as platform operator externally to issue Administrator invitations. First membership activation still requires OPEN-GATE-01; provisioning does not bypass it.

database/seed-demo.sql is optional synthetic data, blocked unless vairas.demo=on. Use it only in an isolated demo database. Its fixed IDs are not live Auth/UM accounts or proof that onboarding works without the activation provider.

## Configure and start

Keep credentials in user-secrets/environment variables or an external secret store. appsettings.json contains only nonsecret defaults.

| Setting | Required value |
|---|---|
| ConnectionStrings:SchoolManagement | Restricted runtime Npgsql connection |
| Dependencies:AuthBaseUrl | HTTPS Authentication origin; default https://localhost:7100/ |
| Dependencies:UserManagementBaseUrl | HTTPS User Management origin; default https://localhost:7001/; set to the actual configured origin |
| Dependencies:ClientId / ClientSecret | Auth workload client school-management and its externally provisioned secret |
| Authentication:Authority / Issuer | Actual trusted HTTPS Auth issuer |
| Authentication:WorkloadClients:<client> | Local list of scopes permitted for that calling workload; empty grants nothing |
| Access:PlatformOperatorUserIds | Existing active **UM UserIds**, not Auth IdentityIds; empty grants no platform rights |
| AllowedHosts | Actual hosting hostname(s) |
| OTEL_EXPORTER_OTLP_ENDPOINT | Optional external telemetry collector |

Environment variable hierarchy uses double underscores, e.g. ConnectionStrings__SchoolManagement. For local configuration:

```powershell
dotnet user-secrets set 'ConnectionStrings:SchoolManagement' '<runtime connection>' --project Vairas.SchoolManagement
dotnet user-secrets set 'Dependencies:ClientSecret' '<externally provisioned workload secret>' --project Vairas.SchoolManagement
dotnet run --project Vairas.SchoolManagement --launch-profile https
```

Local origin: https://localhost:7200. Liveness is /health/live; /health/ready verifies local PostgreSQL schema/RLS and a restricted runtime role. Readiness is not proof that Auth/UM or absent Scheduling/Learning providers are healthy. OpenAPI is available in Development/Testing. No reverse-proxy forwarded headers or cross-origin browser policy is trusted implicitly; configure the actual ingress before external hosting.

## Connect existing Authentication and User Management

Keep their current API routes. Configure:

1. Auth user audiences: preserve existing audiences and add **vairas-school-management**. The user JWT must also include vairas-user-management because School forwards it only to UM's own /api/v1/users/me leaf.
2. Auth workload client **school-management**, scopes **identity.validate profiles.status.read profiles.binding.read**. Generate its Argon2id secret hash with Authentication's existing hash-secret command; store the raw secret only in School's external configuration.
3. UM local workload allowlist for school-management: **profiles.status.read**, **profiles.binding.read**.
4. UM's outgoing client user-management retains **school.profile-provisioning** and its current Auth/session scopes. Set its SchoolBaseUrl to this service. School's local allowlist must permit user-management for **school.profile-provisioning**.
5. Future Scheduling readers need Auth scope-to-audience mappings to **vairas-school-management-internal** for **school.eligibility.read** and **school.participants.read**, plus matching School workload allowlists. These reads alone do not enable the missing activation protocol.

All requests check signed RS256 issuer/audience/lifetime. User sub/sid are Auth identifiers; School resolves UserId/profile IDs through UM, validates the current session, and enforces a 30-second admission budget, including lock waits. Positive activity decisions are not cached across requests. Auth/UM failure denies the new action. Already admitted bounded work may finish; no instantaneous distributed revocation guarantee is claimed.

Staff provisioning validation is local-only to avoid UM→School→UM recursion. School first acceptance separately checks the current issuer's UM activity. The existing UM provider contract lacks the richer proposed issuer-activity response/check; creating an otherwise unprivileged global staff profile is not school membership or activation. See CONTRACT-NOTES.md.

## Commands, retries and errors

All writes require a nonempty UUID Idempotency-Key. Logical uniqueness is **(SchoolId, OperationId)**; the same UUID in another authorized school is independent. Fingerprints bind canonical typed payload, actor, command, target and expected revision. Retry the original path/body/key; changed intent returns 409 OPERATION_KEY_REUSE without disclosing the original payload. Generated IDs are allocated after replay lookup.

Use expectedRevision for updates/accept/revoke. First creates return 201 with Location; authorized replay returns 200 and the currently authorized representation. Read/retry requires fresh authentication and authority. Accepted invitation replay resolves the original accepting result before Pending-only expiration checks and never creates another enrollment. Unused alias keys are not persisted as new acceptances.

Errors use application/problem+json, with code and correlationId: 400 validation; 401 invalid identity/session; 403 denied; 404 unknown/not visible; 409 conflict; 410 authorized expired invitation; 422 own role profile missing; 503 dependency/activation unavailable. Database details, tokens, passwords and profile bodies are not logged or returned. Retained operations provide audit evidence without saving command bodies.

## Verification

```powershell
./scripts/verify.ps1
```

For a standalone clone without the neighboring service sources, run:

```powershell
./scripts/verify.ps1 -SkipIntegration
```

This verifies build, EF snapshot and the 70% unit coverage gate and explicitly reports integration tests as NOT RUN. The full command above requires sibling Auth/UM sources and fails clearly if they are absent, rather than silently passing an empty integration suite.
The default integration backend is a disposable PostgreSQL 17 Testcontainer and therefore needs a running Docker engine **for tests only**. Alternatively supply VAIRAS_TEST_ADMIN_CONNECTION for an explicitly disposable local PostgreSQL cluster. The harness creates uniquely named databases and NOSUPERUSER owner/runtime roles, then removes only those databases/roles. Never supply a production cluster.

```powershell
$env:VAIRAS_TEST_ADMIN_CONNECTION = '<disposable local cluster administrator connection>'
./scripts/verify.ps1
```

Unit tests need no running Auth/UM/DB. Integration tests start actual in-process pipelines for all three services; a test transport replaces network connections and routes JWKS/token/session/profile requests to those pipelines. Tests generate synthetic accounts, workload secrets and a temporary signing certificate. SMTP, live public TLS ingress, production OIDC, broker interoperability and deployment are not covered.

See [VALIDATION.md](VALIDATION.md) for measured coverage, database version and executed checks. The verification script requires a fresh unit coverage report with at least 70% Business lines and fails on build/test/model/audit errors.
