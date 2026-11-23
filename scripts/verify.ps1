param([string]$Configuration = 'Release', [switch]$SkipIntegration)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    if (!$SkipIntegration -and (!(Test-Path '../Vairas.Authentication/Vairas.Authentication/Vairas.Authentication.csproj') -or !(Test-Path '../Vairas.UserManagement/Vairas.UserManagement/Vairas.UserManagement.csproj'))) {
        throw 'Full integration verification needs sibling Authentication and User Management sources. Use -SkipIntegration for standalone build/unit verification.'
    }
    dotnet restore Vairas.SchoolManagement.slnx
    if ($LASTEXITCODE -ne 0) { throw 'Restore or NuGet security audit failed.' }
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'EF tool restore failed.' }
    dotnet build Vairas.SchoolManagement.slnx --no-restore -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    dotnet ef migrations has-pending-model-changes --project Vairas.SchoolManagement.Persistance --startup-project Vairas.SchoolManagement.Persistance --no-build --configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'EF model differs from migration snapshot.' }
    $started = [DateTime]::UtcNow
    $suffix = Get-Date -Format 'yyyyMMdd-HHmmss'
    dotnet test Vairas.SchoolManagement.Tests --no-build --no-restore -c $Configuration --filter 'TestCategory=Unit' --settings coverage.runsettings --collect:'XPlat Code Coverage' --logger "trx;LogFileName=unit-$suffix.trx"
    if ($LASTEXITCODE -ne 0) { throw 'Unit tests failed.' }
    $report = Get-ChildItem Vairas.SchoolManagement.Tests/TestResults -Recurse -Filter coverage.cobertura.xml |
        Where-Object LastWriteTimeUtc -ge $started | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if (!$report) { throw 'No fresh unit coverage report was produced.' }
    [xml]$coverage = Get-Content -Raw -LiteralPath $report.FullName
    $business = $coverage.coverage.packages.package | Where-Object name -eq 'Vairas.SchoolManagement.Business'
    if (!$business) { throw 'Business assembly is missing from coverage.' }
    $rate = [double]::Parse($business.'line-rate', [Globalization.CultureInfo]::InvariantCulture)
    if ($rate -lt 0.70) { throw "Business unit-test line coverage below 70%: $rate" }
    Write-Output ('Business unit-test line coverage: {0:P2}' -f $rate)
    Write-Output $report.FullName
    if ($SkipIntegration) {
        Write-Output 'Integration tests NOT RUN: standalone verification requested.'
    } else {
        dotnet test Vairas.SchoolManagement.Tests --no-build --no-restore -c $Configuration --filter 'TestCategory=Integration' --logger "trx;LogFileName=integration-$suffix.trx"
        if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL/HTTP integration tests failed.' }
    }
} finally { Pop-Location }
