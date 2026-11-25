# Vairas.SchoolManagement — code coverage

**Line coverage 79.27 %** (2095/2643 executable lines), branch coverage 61.19 % — threshold 70 % **met**. Generated 2026-11-25.

| Assembly | Line % | Lines | Branch % |
|---|---:|---:|---:|
| Vairas.SchoolManagement | 57.34 | 504/879 | 34.5 |
| Vairas.SchoolManagement.Business | 88.23 | 1237/1402 | 70.45 |
| Vairas.SchoolManagement.Persistance | 97.79 | 354/362 | 89.13 |

## How it was measured

- Tool: coverlet (`XPlat Code Coverage`) driven by `Backend/local/coverage.py`; settings `Vairas.SchoolManagement/coverage.runsettings` (migrations and test assemblies excluded).
- Test projects: `Vairas.SchoolManagement.Tests`; line hits of all test projects of the service are merged (maximum per line), so shared code is never counted twice.
- Filter: `TestCategory!=Broker`. Broker tests need the Service Bus emulator and are executed (and passed) separately by `local/test_all.py`.
- The numbers are measured line coverage, not a statement about functional completeness; acceptance status is tracked in `Documentation/Final-Verification/AC-Traceability-Matrix.xlsx`.

## Reproduce

```powershell
cd Backend
py -3 local/coverage.py --services Vairas.SchoolManagement --filter "TestCategory!=Broker" --threshold 70
```

## Files

`coverage-summary.json` (machine-readable, per file) and `index.html` (per-file table, lowest coverage first) are in this folder.

## Least covered files

| File | Line % | Lines |
|---|---:|---:|
| `Vairas.SchoolManagement.Business/ISchoolService.cs` | 0.0 | 0/3 |
| `Vairas.SchoolManagement.Business/IUserActivity.cs` | 0.0 | 0/1 |
| `Vairas.SchoolManagement.Business/NotificationRecipientsService.cs` | 0.0 | 0/30 |
| `Security/LessonActions.cs` | 0.0 | 0/61 |
| `Integrations/CoordinationQueue.cs` | 0.0 | 0/5 |
| `Controllers/AssistantAccessController.cs` | 0.0 | 0/2 |
| `Controllers/DocumentIntegrationController.cs` | 0.0 | 0/2 |
| `Controllers/LearningIntegrationController.cs` | 0.0 | 0/3 |
