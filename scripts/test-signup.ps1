<#
.SYNOPSIS
    End-to-end signup test: clears prior state, posts a signup, polls status, and
    reports exactly what landed in the tenant database.

.DESCRIPTION
    Runs the real services — nothing is mocked. The sequence is:

        0. Report which ports are listening, so "service not running" is
           distinguished from "service returned an error".
        1. Clear mst.Customers / Organizations / CustomerDatabases.
        2. POST /api/customers/signup.
        3. Poll /api/customers/{id}/status until CanLogin, or until the database
           row goes Failed, or the timeout expires.
        4. Dump the admin tables, then every table in the tenant database with
           its row count, so a schema that was cloned but never seeded is
           visible rather than inferred.

    Provisioning is asynchronous, so a 202 from step 2 means only that the job
    was queued. Step 4 is what actually answers whether the tenant database was
    built and seeded.

.PARAMETER BaseUrl
    Where to send the API calls. Defaults to the Gateway. Point it at
    http://localhost:4504 to bypass the Gateway, or http://localhost:4200 to go
    through the Angular dev proxy.

.PARAMETER SkipClean
    Leave existing customers in place instead of deleting them first.

.EXAMPLE
    ./scripts/test-signup.ps1

.EXAMPLE
    ./scripts/test-signup.ps1 -BaseUrl http://localhost:4504
#>

[CmdletBinding()]
param(
    [string] $BaseUrl = 'http://localhost:4500',
    [string] $PostgresHost = 'localhost',
    [int]    $PostgresPort = 5432,
    [string] $PostgresUser = 'postgres',
    [string] $PostgresPassword = '123',
    [string] $AdminDatabase = 'EP_Admin',
    [int]    $TimeoutSeconds = 120,
    [switch] $SkipClean
)

$ErrorActionPreference = 'Continue'
$env:PGPASSWORD = $PostgresPassword
$sql = $PSScriptRoot

function Step ([string] $m) { Write-Host "`n=== $m" -ForegroundColor Cyan }
function Ok   ([string] $m) { Write-Host "    $m" -ForegroundColor Green }
function Warn ([string] $m) { Write-Host "    $m" -ForegroundColor Yellow }
function Bad  ([string] $m) { Write-Host "    $m" -ForegroundColor Red }

# Not $args: that is an automatic variable and reassigning it is asking for
# trouble. Everything here stays Windows PowerShell 5.1 compatible, because the
# VS Code task runs powershell.exe, not pwsh — no ?. and no ??.
function Invoke-Psql {
    param([string] $Database, [string] $File, [string] $Command)
    $psqlArgs = @('-h', $PostgresHost, '-p', $PostgresPort, '-U', $PostgresUser, '-d', $Database)
    if ($File)    { $psqlArgs += @('-f', $File) }
    if ($Command) { $psqlArgs += @('-c', $Command) }
    & psql @psqlArgs
}

# ---------------------------------------------------------------- 0. listeners
Step 'Ports listening'
$ports = [ordered]@{
    4200 = 'Angular dev server'
    4500 = 'Gateway'
    4501 = 'Accounting.Api'
    4502 = 'Customer.Api'
    4503 = 'Inventory.Api'
    4504 = 'Master.Api'
    4505 = 'Purchase.Api'
    4506 = 'Reporting.Api'
    4507 = 'Sales.Api'
    5432 = 'PostgreSQL'
}
# GetEnumerator, not $ports[$p]: an OrderedDictionary indexed with an integer
# treats it as a position, not a key, so $ports[4200] looks for the 4200th entry
# and returns nothing. That silently blanked every label and left the guard below
# comparing against empty strings, so it never fired.
$missing = @()
foreach ($entry in $ports.GetEnumerator()) {
    $open = (Test-NetConnection -ComputerName localhost -Port $entry.Key `
                -WarningAction SilentlyContinue -InformationLevel Quiet)
    if ($open) { Ok "$($entry.Key)  $($entry.Value)" }
    else       { Bad "$($entry.Key)  $($entry.Value)  NOT LISTENING"; $missing += $entry.Value }
}

# Seeding is synchronous HTTP from Master to these three. If any is down the
# seeder reports it unseeded and ProvisioningWorker throws, so the run is
# pointless before it starts.
$required = @('Gateway', 'Master.Api', 'Accounting.Api', 'Inventory.Api', 'Sales.Api', 'PostgreSQL')
$blocked  = $required | Where-Object { $missing -contains $_ }
if ($blocked) {
    Bad ''
    Bad "Cannot test: $($blocked -join ', ') not running."
    Bad 'Master calls Accounting, Inventory and Sales over HTTP to seed the new'
    Bad 'organization. With any of them down, provisioning always fails.'
    return
}

# ------------------------------------------------------------------- 1. clean
if (-not $SkipClean) {
    Step 'Clearing previous signups'
    Invoke-Psql -Database $AdminDatabase -File (Join-Path $sql 'clean-signup.sql')

    # Tenant databases outlive their rows: dropping the CustomerDatabases row
    # leaves the database itself behind, and the next signup generates the same
    # code and fails on CREATE DATABASE. Names are the generated customer code,
    # never anything else, so the pattern is safe.
    $orphans = & psql -h $PostgresHost -p $PostgresPort -U $PostgresUser -d postgres -t -A `
        -c "SELECT datname FROM pg_database WHERE datname LIKE 'IN%';" 2>$null
    foreach ($db in $orphans) {
        if ([string]::IsNullOrWhiteSpace($db)) { continue }
        $name = $db.Trim()
        Warn "dropping orphaned tenant database $name"

        # Written to a file, not passed with -c. The database name is PascalCase
        # and must stay double-quoted, and PowerShell strips those quotes on the
        # way to psql.exe — so DROP DATABASE IF EXISTS "IN0000000001" arrived
        # unquoted, Postgres folded it to in0000000001, found nothing, and
        # IF EXISTS turned the whole thing into a silent no-op. The drop appeared
        # to succeed and the next signup died on 42P04.
        #
        # ALLOW_CONNECTIONS false before terminating, because the running services
        # hold Npgsql pools open and reconnect within milliseconds, so terminate
        # alone leaves DROP racing them.
        $dropSql = Join-Path $sql 'drop-tenant.generated.sql'
        @"
ALTER DATABASE "$name" WITH ALLOW_CONNECTIONS false;
SELECT pg_terminate_backend(pid) FROM pg_stat_activity
 WHERE datname = '$name' AND pid <> pg_backend_pid();
DROP DATABASE "$name";
"@ | Set-Content -Path $dropSql -Encoding ASCII

        $dropOutput = & psql -h $PostgresHost -p $PostgresPort -U $PostgresUser -d postgres `
                             -v ON_ERROR_STOP=1 -f $dropSql 2>&1
        $stillThere = & psql -h $PostgresHost -p $PostgresPort -U $PostgresUser -d postgres -t -A `
                             -c "SELECT 1 FROM pg_database WHERE datname = '$name';" 2>$null
        if ($stillThere) {
            Bad "could not drop $name — the next signup will fail with 42P04. psql said:"
            Bad "  $dropOutput"
        } else {
            Ok "dropped $name"
        }
    }
    Ok 'Cleared'
}

# ------------------------------------------------------------------ 2. signup
Step "POST $BaseUrl/api/customers/signup"

$body = @{
    displayName             = 'sanjay b'
    email                   = 'sanjay.b.8124@gmail.com'
    password                = 'sanjayay'
    mobileNumber            = '7305852119'
    companyName             = 'sanjay'
    organizationName        = 'sanjay'
    financialYearStartMonth = 4
    baseCurrency            = 'INR'
    gstin                   = ''
    pan                     = ''
    tan                     = ''
    tin                     = ''
    cin                     = ''
    udyamNumber             = ''
    countryId               = 1
    stateId                 = 1832
    addressLine1            = '......'
    addressLine2            = '.....'
    city                    = 'chennai'
    postalCode              = '600126'
} | ConvertTo-Json

$customerId = $null
try {
    $response = Invoke-WebRequest -Uri "$BaseUrl/api/customers/signup" -Method Post `
        -ContentType 'application/json' -Body $body -UseBasicParsing -TimeoutSec 60
    Ok "HTTP $($response.StatusCode)"
    Write-Host "    $($response.Content)" -ForegroundColor Gray
    $customerId = ($response.Content | ConvertFrom-Json).customerId
}
catch {
    Bad $_.Exception.Message
    $webResponse = $_.Exception.Response
    if ($webResponse) {
        Bad "HTTP $([int]$webResponse.StatusCode)"
        try {
            $reader = New-Object System.IO.StreamReader($webResponse.GetResponseStream())
            Bad $reader.ReadToEnd()
        } catch { }
    }
}

# ------------------------------------------------------------------- 3. status
if ($customerId) {
    Step "Polling $BaseUrl/api/customers/$customerId/status"
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $final = $null
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 3
        try {
            $s = Invoke-RestMethod -Uri "$BaseUrl/api/customers/$customerId/status" -TimeoutSec 30
            # CustomerStatusResponse: customerStatus, databaseStatus, canLogin.
            Write-Host "    customer=$($s.customerStatus)  database=$($s.databaseStatus)  canLogin=$($s.canLogin)" -ForegroundColor Gray
            if ($s.canLogin -or $s.databaseStatus -in 'Ready', 'Failed') { $final = $s; break }
        }
        catch {
            Bad "status call failed: $($_.Exception.Message)"
            break
        }
    }
    if ($final) {
        if ($final.canLogin) { Ok  "Final: customer=$($final.customerStatus) database=$($final.databaseStatus) canLogin=True" }
        else                 { Bad "Final: customer=$($final.customerStatus) database=$($final.databaseStatus) canLogin=False" }
    }
    else { Warn "Still not settled after ${TimeoutSeconds}s" }
}

# --------------------------------------------------------------- 4. inspection
Step "Admin database ($AdminDatabase)"
Invoke-Psql -Database $AdminDatabase -File (Join-Path $sql 'inspect-admin.sql')

Step 'Databases on the server'
Invoke-Psql -Database 'postgres' -Command @'
SELECT datname, pg_size_pretty(pg_database_size(datname)) AS size
FROM pg_database WHERE datname NOT IN ('postgres','template0','template1') ORDER BY 1;
'@

# The tenant database name is generated, so read it back rather than guess.
# From a file, not -c: PascalCase identifiers need double quotes and PowerShell
# mangles those on the way to psql.exe, so mst."CustomerDatabases" arrived as
# "mst.customerdatabases" and the lookup silently returned nothing.
# -t drops the header and row count, -A the column padding.
$tenantDb = (& psql -h $PostgresHost -p $PostgresPort -U $PostgresUser -d $AdminDatabase `
                    -t -A -f (Join-Path $sql 'tenant-name.sql') 2>$null)
if ($tenantDb) { $tenantDb = ($tenantDb | Where-Object { $_ -and $_.Trim() } | Select-Object -First 1).Trim() }

Step 'Template database (EP_Tenant_Template) — cloned for every tenant'
Invoke-Psql -Database 'EP_Tenant_Template' -File (Join-Path $sql 'inspect-tenant.sql')

if ($tenantDb) {
    Step "Tenant database ($tenantDb)"
    Invoke-Psql -Database $tenantDb -File (Join-Path $sql 'inspect-tenant.sql')
} else {
    Warn 'No row in mst."CustomerDatabases" — provisioning never got as far as creating one.'
}

Step 'Done'
