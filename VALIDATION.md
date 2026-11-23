# School Management verification — 2026-11-23

| Check | Result |
|---|---|
| .NET 10 Release build | PASS, zero warnings/errors |
| scripts/verify.ps1 | PASS: restore/audit, build, EF snapshot check, unit coverage gate, integration tests |
| Unit tests | **40 passed / 0 failed / 0 skipped** |
| Business coverage from unit tests only | **95.23% lines / 81.83% branches**, Coverlet Cobertura |
| PostgreSQL / real HTTP integration tests | **16 passed / 0 failed / 0 skipped** |
| EF model vs migration snapshot | PASS, no pending model changes |
| Canonical schema | 13 tables, 137 columns, seven enums, four exclusion constraints |
| Runtime isolation | PASS: RLS, pooled scope reset, DELETE/CREATE/TEMP denied |
| OpenAPI | PASS: 29 operations, Microsoft.OpenApi parsing/default validation |
| NuGet vulnerability audit | PASS: five solution projects, direct/transitive dependencies, no reported vulnerable packages |
| Release publish | PASS, ignored .cache/publish |

## Evidence

- [Unit coverage](evidence/unit-coverage.cobertura.xml), [unit TRX](evidence/unit.trx), [integration TRX](evidence/integration.trx).
- [Machine-readable summary](evidence/verification-summary.json), [NuGet audit](evidence/dependency-audit.json).
- [OpenAPI export](api/school-management.openapi.json), [source hashes](source-manifest.json).
- Detailed local command logs: ignored .cache/verification-accepted.log and .cache/integration-final.log.

Coverage includes the complete Business assembly, excluding generated obj files. **599 of 629 executable Business lines** are covered. Integration coverage is not used to meet the 70% unit threshold.

## Scenarios exercised

- Actual Authentication, User Management and School application pipelines, connected by an in-process transport: real issuer/JWKS discovery, workload-token acquisition, JWT/session validation and UM self/profile reads.
- Exact invitations authorize existing UM TeacherProfile provisioning; global profiles alone grant no school access. Absent Scheduling activation returns 503 before local business changes.
- School-scoped replay, changed-intent conflict, identical keys in independent tenants and six concurrent identical creates yielding one resource.
- Accept/revoke race yields one terminal invitation result; unchanged staff membership is not duplicated.
- Revoked Auth session and authoritative UM Inactive state deny the next School admission. UM state changes are fixture-controlled; this is not a test of UM's deactivation command.
- User/workload audience separation, required scopes, local client allowlists, foreign resource hiding and exact-target self discovery.
- Native period overlap rejection, immutable roster evidence, RLS and runtime schema/object/temporary-table restrictions.
- Unit tests cover exact historical roster revisions, full-interval filtering, duplicate canonical students, assigned-teacher scope, expiry, issuer loss, role prerequisites, freshness deadlines and missing planning dependencies.

## Environment and limits

Executed against **PostgreSQL 14.15 on Windows x64**, using a separate cluster listening only on loopback. This is real PostgreSQL, not PGlite, and is not proof of PostgreSQL 17 execution. PostgreSQL 17 Testcontainers is the default test backend when Docker is available; it was not executed here.

Each run creates three uniquely named databases with separate nonsuperuser owner/runtime roles and removes its databases/roles afterward. Schema ownership is explicitly assigned before migration, allowing PUBLIC creation rights to be revoked on PostgreSQL 14 as well. Accounts, workload secrets, passwords and signing certificates are generated synthetic test data.

The frozen Initial.sql SHA-256 matches the canonical input byte-for-byte. Companion exclusions execute inside the EF migration transaction. The additional guards and self-discovery function are documented implementation changes; they do not change domain ownership.

Public TLS/ingress, production provider configuration, SMTP/OIDC delivery, live messaging, load/SLA/backup recovery, Scheduling activation, Learning evidence, Git publication/history and production deployment are **not completed or verified**. New membership/enrollment activation remains unavailable until its required integration exists.

## Repository packaging verification — 2026-11-24

A standalone Git checkout without sibling Authentication/User Management sources passed `scripts/verify.ps1 -SkipIntegration`: Release build, unchanged EF model and 40 unit tests, with 95.23% Business line coverage. Cross-service integration tests were explicitly NOT RUN in this standalone check; the complete 16-test run above remains the 2026-11-23 result. Compilation with `EnableServiceIntegrationTests=true` and both sibling sources also passed.

The private repository is https://github.com/MikitaBeliayev-Diploma/Vairas.SchoolManagement. Initial import records the actual publication date; no historical development commits were fabricated. The staged tree excludes build/cache/test-output directories, local environment files and private-key/certificate files. Known GitHub credential/private-key marker scanning found no matches. The reviewer requires read access to this private repository.