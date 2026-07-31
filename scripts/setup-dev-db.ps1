<#
.SYNOPSIS
    Creates the local databases, generates EF Core migrations and applies them.

.DESCRIPTION
    One command to go from a fresh clone to a running local stack:

        1. Creates retailerp_master, retailerp_design, retailerp_test and
           IN0000000001 via scripts/setup-dev-db.sql.
        2. Adds an InitialCreate migration to each service that has a DbContext,
           but only where no migration exists yet.
        3. Applies the migrations to the right database for each service.

    Safe to re-run. Step 1 skips databases that exist, step 2 skips services that
    already have a migration, and step 3 is a no-op once the schema is current.

    Only four services have a DbContext today: Master, Platform, Identity and
    Accounting. The other eight (Banking, Contacts, Crm, Inventory, Purchase,
    Reporting, Sales, Support) have empty Repository projects, so EF has nothing
    to generate. They are listed as skipped rather than silently ignored.

.PARAMETER PostgresUser
    Postgres superuser. Defaults to postgres.

.PARAMETER PostgresPassword
    Password for that user. Defaults to the local development password.

.PARAMETER SkipMigrations
    Create the databases only; do not touch EF Core.

.EXAMPLE
    ./scripts/setup-dev-db.ps1

.EXAMPLE
    ./scripts/setup-dev-db.ps1 -PostgresPassword 'something-else'
#>

[CmdletBinding()]
param(
    [string] $PostgresHost = 'localhost',
    [int]    $PostgresPort = 5432,
    [string] $PostgresUser = 'postgres',
    [string] $PostgresPassword = '123',
    [switch] $SkipMigrations
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$backend  = Join-Path $repoRoot 'backend'

function Write-Step  ([string] $Message) { Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Ok    ([string] $Message) { Write-Host "    $Message" -ForegroundColor Green }
function Write-Skip  ([string] $Message) { Write-Host "    $Message" -ForegroundColor DarkGray }

# Services that actually have a DbContext, and which database each migrates into.
#
#   master  -> retailerp_master, via the MasterDatabase connection string
#   tenant  -> retailerp_design, via DesignTimeDatabase; these are the per-customer
#              schemas, which in production live in each Customer's own database
$services = @(
    [pscustomobject]@{ Name = 'Master';     Target = 'master'; Repository = 'Api/Master/Master.Repository';         Startup = 'Api/Master/Master.Api' }
    [pscustomobject]@{ Name = 'Platform';   Target = 'master'; Repository = 'Api/Platform/Platform.Repository';     Startup = 'Api/Platform/Platform.Api' }
    [pscustomobject]@{ Name = 'Identity';   Target = 'master'; Repository = 'Api/Identity/Identity.Repository';     Startup = 'Api/Identity/Identity.Api' }
    [pscustomobject]@{ Name = 'Accounting'; Target = 'tenant'; Repository = 'Api/Accounting/Accounting.Repository'; Startup = 'Api/Accounting/Accounting.Api' }
)

$notYetBuilt = @('Banking', 'Contacts', 'Crm', 'Inventory', 'Purchase', 'Reporting', 'Sales', 'Support')

# --------------------------------------------------------------- prerequisites
Write-Step 'Checking prerequisites'

if (-not (Get-Command psql -ErrorAction SilentlyContinue)) {
    throw "psql is not on PATH. It ships with PostgreSQL; add its bin directory " +
          "(typically C:\Program Files\PostgreSQL\17\bin) to PATH and reopen the terminal."
}
Write-Ok "psql found: $((Get-Command psql).Source)"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet is not on PATH. Install the .NET 10 SDK.'
}
Write-Ok "dotnet found: $(dotnet --version)"

if (-not $SkipMigrations) {
    # `dotnet ef` is a local or global tool, not part of the SDK.
    $efInstalled = (dotnet tool list --global 2>$null | Select-String -SimpleMatch 'dotnet-ef')
    if (-not $efInstalled) {
        Write-Host '    dotnet-ef not installed; installing globally...' -ForegroundColor Yellow
        dotnet tool install --global dotnet-ef
        if ($LASTEXITCODE -ne 0) { throw 'Failed to install dotnet-ef.' }
    }
    Write-Ok 'dotnet-ef available'
}

# ------------------------------------------------------------------- databases
Write-Step 'Creating databases'

# PGPASSWORD rather than a password in the connection string: it does not appear
# in the process list. Scoped to this process only.
$env:PGPASSWORD = $PostgresPassword

$sqlFile = Join-Path $PSScriptRoot 'setup-dev-db.sql'
psql -h $PostgresHost -p $PostgresPort -U $PostgresUser -d postgres -v ON_ERROR_STOP=1 -f $sqlFile
if ($LASTEXITCODE -ne 0) {
    throw "psql failed. Check that PostgreSQL is running on ${PostgresHost}:${PostgresPort} and that the password is correct."
}
Write-Ok 'Databases ready'

if ($SkipMigrations) {
    Write-Step 'Done (migrations skipped)'
    return
}

# ------------------------------------------------------------------ migrations
Write-Step 'Generating migrations'

# Must be set before `migrations add`, not just before `database update`. EF builds
# the application host to find the DbContext, and a WebApplication with no
# environment set defaults to Production — where every connection string is blank
# by design, so the startup guard throws and EF reports only "Unable to create a
# DbContext", which points nowhere near the real cause.
$env:ASPNETCORE_ENVIRONMENT = 'Development'

foreach ($service in $services) {
    $repositoryPath = Join-Path $backend $service.Repository
    $migrationsPath = Join-Path $repositoryPath 'Migrations'

    # An empty Migrations directory is committed for each service, so test for
    # actual migration files rather than for the directory.
    $existing = @(Get-ChildItem -Path $migrationsPath -Filter '*.cs' -ErrorAction SilentlyContinue)
    if ($existing.Count -gt 0) {
        Write-Skip "$($service.Name): migration already exists, skipping"
        continue
    }

    Write-Host "    $($service.Name): adding InitialCreate..." -ForegroundColor Yellow
    dotnet ef migrations add InitialCreate `
        --project   (Join-Path $backend $service.Repository) `
        --startup-project (Join-Path $backend $service.Startup) `
        --output-dir Migrations
    if ($LASTEXITCODE -ne 0) { throw "Failed to add migration for $($service.Name)." }
    Write-Ok "$($service.Name): InitialCreate added"
}

# ---------------------------------------------------------------------- update
Write-Step 'Applying migrations'

foreach ($service in $services) {
    $database = if ($service.Target -eq 'master') { 'retailerp_master' } else { 'retailerp_design' }
    Write-Host "    $($service.Name) -> $database" -ForegroundColor Yellow

    dotnet ef database update `
        --project   (Join-Path $backend $service.Repository) `
        --startup-project (Join-Path $backend $service.Startup)
    if ($LASTEXITCODE -ne 0) { throw "Failed to apply migrations for $($service.Name)." }
    Write-Ok "$($service.Name): schema current, seed data applied"
}

# ----------------------------------------------------------------- verification
Write-Step 'Verifying seed data'

# Query lives in a file: identifiers are PascalCase and need double quotes,
# which PowerShell mangles on the way to psql.exe when passed with -c.
psql -h $PostgresHost -p $PostgresPort -U $PostgresUser -d retailerp_master `
     -f (Join-Path $PSScriptRoot 'verify-seed.sql')

Write-Step 'Not yet built'
Write-Skip "No DbContext, so no migration: $($notYetBuilt -join ', ')"
Write-Skip 'Chart of Accounts and Tax Master seed per organization at signup, not via HasData.'

Write-Step 'Done'
Write-Host '    Press F5 in VS Code and pick "Everything (APIs + Gateway + Web)".' -ForegroundColor Green
