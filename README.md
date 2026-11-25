# Vairas School Management

Tenant owner of schools, branches, memberships, invitations, groups, enrollments, scheduling and learning
coordination, document-source reads and the assistant-access audit (ASP.NET Core 10, PostgreSQL, EF Core/Npgsql).
User Management owns global profiles; Authentication owns credentials; Scheduling and Learning own lessons and
teaching progress.

## Projects

| Project | Responsibility |
|---|---|
| `Vairas.SchoolManagement` | HTTP API: controllers, `Configuration/Configure*`, security, dependency clients, workers |
| `Vairas.SchoolManagement.Business` | Application services and ports |
| `Vairas.SchoolManagement.Domain` | Entities and rules |
| `Vairas.SchoolManagement.Persistance` | `SchoolDbContext`, migrations, store |
| `Vairas.SchoolManagement.Tests` | NUnit tests (PostgreSQL via Testcontainers, in-process User Management) |

## Dependencies

PostgreSQL 17, Authentication, User Management; optionally Scheduling, Learning and a Service Bus broker for the
Learning outbox and the `UserDeactivated` inbox. Local origin `https://localhost:7200`.

## Essential configuration

Options are bound and validated at startup (`Configuration/ConfigureOptions.cs`). Use `__` in environment variables.

| Section | Notes |
|---|---|
| `ConnectionStrings:SchoolManagement` | required, restricted runtime role |
| `ConnectionStrings:LearningDelivery` | separate delivery role, used only when the Learning outbox is enabled |
| `Authentication` | HTTPS `Authority`/`Issuer`, audiences, `WorkloadClients` allowlist |
| `Dependencies` | HTTPS `AuthBaseUrl`/`UserManagementBaseUrl`; `ClientId` and `ClientSecret` required |
| `Access` | `PlatformOperatorUserIds`, `LessonCorrectionUserIds` (no empty IDs) |
| `SchedulingDependency` | `Enabled`, `BaseUrl`, `LearningBaseUrl` |
| `LearningServiceBus`, `Inbox` | broker settings; disabled integrations need no credentials |

## Run locally

```powershell
dotnet run --project Vairas.SchoolManagement --launch-profile https
```

The complete local stack is started from `../local/Start.ps1`. Database roles, grants and the synthetic demo seed are
in `database/`; migrations are applied by the migration role, never on startup.

## Tests

```powershell
dotnet test Vairas.SchoolManagement.slnx        # Docker required for PostgreSQL tests
./scripts/verify.ps1 -SkipIntegration           # unit tests with the coverage gate
```

Contract notes: [CONTRACT-NOTES.md](CONTRACT-NOTES.md). API exports: `api/`. Extended notes, validation reports and
evidence: `../../Documentation/Backend/Vairas.SchoolManagement/`.

## Tests and coverage

Requirements: .NET SDK 10 and Docker (PostgreSQL integration tests start a throw-away container via Testcontainers).

```powershell
dotnet test Vairas.SchoolManagement.slnx                  # unit + integration tests
dotnet test Vairas.SchoolManagement.slnx --collect:"XPlat Code Coverage" --settings coverage.runsettings --results-directory TestResults   # measure coverage
```

**Coverage report:** [docs/coverage/README.md](docs/coverage/README.md) (summary, per-assembly table, least covered files); machine-readable `docs/coverage/coverage-summary.json` and `docs/coverage/index.html` (per-file table). The raw cobertura XML of a local run is written to `TestResults/`.