<#
.SYNOPSIS
    Generates and applies EF Core migrations for the Admin database (EP_Admin).

.DESCRIPTION
    This script streamlines working with the Admin database (EP_Admin) which uses
    the AdminDbContext in the Master service. 
    
    If you provide a -MigrationName, it will generate a new migration first.
    It always finishes by applying pending migrations to the database.

.PARAMETER MigrationName
    The name of the new migration to generate (e.g., "AddUserTable"). 
    If omitted, the script will only apply existing pending migrations.

.EXAMPLE
    ./scripts/migrate-admin.ps1
    Applies any pending migrations to the Admin database.

.EXAMPLE
    ./scripts/migrate-admin.ps1 -MigrationName "AddUserTable"
    Generates a new migration named "AddUserTable" and applies it.
#>
[CmdletBinding()]
param(
    [string] $MigrationName = ''
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$backend  = Join-Path $repoRoot 'backend'

$repository = Join-Path $backend 'Api/Master/Master.Repository'
$startup    = Join-Path $backend 'Api/Master/Master.Api'
$context    = 'AdminDbContext'
$outputDir  = 'Migrations/Master'

# We must set this so the host builder uses the Development appsettings,
# otherwise it defaults to Production and throws because the connection string is empty.
$env:ASPNETCORE_ENVIRONMENT = 'Development'

if ($MigrationName) {
    Write-Host "`n=== Generating migration: $MigrationName ===" -ForegroundColor Cyan
    dotnet ef migrations add $MigrationName `
        --project $repository `
        --startup-project $startup `
        --context $context `
        --output-dir $outputDir

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to generate migration '$MigrationName'."
    }
    Write-Host "    Migration generated successfully.`n" -ForegroundColor Green
}

Write-Host "`n=== Applying migrations to EP_Admin (AdminDbContext) ===" -ForegroundColor Cyan
dotnet ef database update `
    --project $repository `
    --startup-project $startup `
    --context $context

if ($LASTEXITCODE -ne 0) {
    throw "Failed to apply migrations."
}

Write-Host "    Admin database is up to date!" -ForegroundColor Green
