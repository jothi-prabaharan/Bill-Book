<#
.SYNOPSIS
    Generates EF Core migrations if missing. Databases are created and migrated automatically by the APIs on startup.

.DESCRIPTION
    One command to go from a fresh clone to having migration files ready.
    The APIs (Master and Accounting) will handle creating EP_Admin, EP_Design, EP_Test and IN0000000001,
    applying migrations, and seeding data on startup.

.EXAMPLE
    ./scripts/setup-dev-db.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$backend  = Join-Path $repoRoot 'backend'

function Write-Step  ([string] $Message) { Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Ok    ([string] $Message) { Write-Host "    $Message" -ForegroundColor Green }
function Write-Skip  ([string] $Message) { Write-Host "    $Message" -ForegroundColor DarkGray }

$services = @(
    [pscustomobject]@{ Name = 'Master';     Target = 'master'; Repository = 'Api/Master/Master.Repository';         Startup = 'Api/Master/Master.Api'; Context = 'AdminDbContext'; OutputDir = 'Migrations/Master' }
    [pscustomobject]@{ Name = 'Contacts';   Target = 'tenant'; Repository = 'Api/Master/Master.Repository';         Startup = 'Api/Master/Master.Api'; Context = 'ContactsDbContext'; OutputDir = 'Migrations/Contacts' }
    [pscustomobject]@{ Name = 'Accounting'; Target = 'tenant'; Repository = 'Api/Accounting/Accounting.Repository'; Startup = 'Api/Accounting/Accounting.Api'; Context = 'AccountingDbContext'; OutputDir = 'Migrations' }
)

$notYetBuilt = @('Banking', 'Contacts', 'Crm', 'Inventory', 'Purchase', 'Reporting', 'Sales', 'Support')

# --------------------------------------------------------------- prerequisites
Write-Step 'Checking prerequisites'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet is not on PATH. Install the .NET 10 SDK.'
}
Write-Ok "dotnet found: $(dotnet --version)"

# `dotnet ef` is a local or global tool, not part of the SDK.
$efInstalled = (dotnet tool list --global 2>$null | Select-String -SimpleMatch 'dotnet-ef')
if (-not $efInstalled) {
    Write-Host '    dotnet-ef not installed; installing globally...' -ForegroundColor Yellow
    dotnet tool install --global dotnet-ef
    if ($LASTEXITCODE -ne 0) { throw 'Failed to install dotnet-ef.' }
}
Write-Ok 'dotnet-ef available'

# ------------------------------------------------------------------ migrations
Write-Step 'Generating migrations'

$env:ASPNETCORE_ENVIRONMENT = 'Development'

foreach ($service in $services) {
    $repositoryPath = Join-Path $backend $service.Repository
    $migrationsPath = Join-Path $repositoryPath $service.OutputDir

    $existing = @(Get-ChildItem -Path $migrationsPath -Filter '*.cs' -ErrorAction SilentlyContinue)
    if ($existing.Count -gt 0) {
        Write-Skip "$($service.Name): migration already exists, skipping"
        continue
    }

    Write-Host "    $($service.Name): adding InitialCreate..." -ForegroundColor Yellow
    dotnet ef migrations add InitialCreate `
        --project   (Join-Path $backend $service.Repository) `
        --startup-project (Join-Path $backend $service.Startup) `
        --context   $service.Context `
        --output-dir $service.OutputDir
    if ($LASTEXITCODE -ne 0) { throw "Failed to add migration for $($service.Name)." }
    Write-Ok "$($service.Name): InitialCreate added"
}

Write-Step 'Not yet built'
Write-Skip "No DbContext, so no migration: $($notYetBuilt -join ', ')"

Write-Step 'Done'
Write-Host '    Migrations are ready. Press F5 in VS Code and pick "Everything (APIs + Gateway + Web)".' -ForegroundColor Green
Write-Host '    The APIs will automatically create the databases, apply migrations, and seed data on startup.' -ForegroundColor Green
