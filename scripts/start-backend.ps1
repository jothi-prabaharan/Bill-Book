<#
.SYNOPSIS
    Starts the backend services signup needs, and waits until each is listening.

.DESCRIPTION
    Signup is not a single-service operation. Master receives the request, but
    provisioning then calls Accounting, Inventory and Sales over HTTP to seed the
    new organization, and ProvisioningWorker fails the whole job if any of them
    cannot be reached. So all of them have to be up before a test means anything.

    Each service is launched detached with its console output redirected to
    scripts/logs/<service>.log, so a service that dies during startup leaves its
    stack trace behind instead of vanishing with its window.

    Ports come from each project's Properties/launchSettings.json.

.PARAMETER Stop
    Stop previously started services instead of starting them.

.EXAMPLE
    ./scripts/start-backend.ps1

.EXAMPLE
    ./scripts/start-backend.ps1 -Stop
#>

[CmdletBinding()]
param(
    [switch] $Stop,
    [int]    $TimeoutSeconds = 120
)

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path -Parent $PSScriptRoot
$logDir   = Join-Path $PSScriptRoot 'logs'
$pidFile  = Join-Path $logDir 'backend-pids.txt'

function Step ([string] $m) { Write-Host "`n=== $m" -ForegroundColor Cyan }
function Ok   ([string] $m) { Write-Host "    $m" -ForegroundColor Green }
function Warn ([string] $m) { Write-Host "    $m" -ForegroundColor Yellow }
function Bad  ([string] $m) { Write-Host "    $m" -ForegroundColor Red }

# Gateway first so it is up before anything proxies through it; Master last
# because it is the one that calls the others.
$services = @(
    @{ Name = 'Gateway';        Port = 4500; Project = 'backend/Gateway/Gateway.Api' }
    @{ Name = 'Accounting.Api'; Port = 4501; Project = 'backend/Api/Accounting/Accounting.Api' }
    @{ Name = 'Inventory.Api';  Port = 4503; Project = 'backend/Api/Inventory/Inventory.Api' }
    @{ Name = 'Sales.Api';      Port = 4507; Project = 'backend/Api/Sales/Sales.Api' }
    @{ Name = 'Master.Api';     Port = 4504; Project = 'backend/Api/Master/Master.Api' }
)

function Test-Port ([int] $Port) {
    Test-NetConnection -ComputerName localhost -Port $Port `
        -WarningAction SilentlyContinue -InformationLevel Quiet
}

# ------------------------------------------------------------------------ stop
if ($Stop) {
    Step 'Stopping services'

    # By port, not by recorded PID. `dotnet run` builds the project and then
    # launches the app as a *child* process, so the PID this script captured is
    # the short-lived wrapper; killing it leaves the app holding the port. The
    # process that owns the listening socket is the one to stop.
    foreach ($s in $services) {
        $owners = @()
        try {
            $owners = @(Get-NetTCPConnection -LocalPort $s.Port -State Listen -ErrorAction SilentlyContinue |
                        Select-Object -ExpandProperty OwningProcess -Unique)
        } catch { }

        if ($owners.Count -eq 0) { Warn "$($s.Name): not running"; continue }

        foreach ($owner in $owners) {
            $proc = Get-Process -Id $owner -ErrorAction SilentlyContinue
            if ($proc) {
                Stop-Process -Id $owner -Force -ErrorAction SilentlyContinue
                Ok "$($s.Name): stopped $($proc.ProcessName) PID $owner on $($s.Port)"
            }
        }
    }

    # Any dotnet run wrappers this script started that are still hanging around.
    if (Test-Path $pidFile) {
        foreach ($id in Get-Content $pidFile) {
            $proc = Get-Process -Id $id -ErrorAction SilentlyContinue
            if ($proc) { Stop-Process -Id $id -Force -ErrorAction SilentlyContinue; Ok "stopped wrapper PID $id" }
        }
        Remove-Item $pidFile -Force
    }

    Start-Sleep -Seconds 2
    foreach ($s in $services) {
        if (Test-Port $s.Port) { Bad "$($s.Name) STILL listening on $($s.Port)" }
    }
    return
}

# ----------------------------------------------------------------------- start
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }
$startedPids = @()

# Inherited by every child. Without it the services default to Production, where
# every connection string and Jwt:SigningKey is deliberately blank, and each one
# dies at startup on its own configuration guard. The launch profile normally
# sets this, but it is set here too so the run does not depend on that.
$env:ASPNETCORE_ENVIRONMENT = 'Development'

Step 'Starting services'
foreach ($s in $services) {
    if (Test-Port $s.Port) {
        Warn "$($s.Name) already listening on $($s.Port), leaving it alone"
        continue
    }

    $projectPath = Join-Path $repoRoot $s.Project
    if (-not (Test-Path $projectPath)) {
        Bad "$($s.Name): project not found at $projectPath"
        continue
    }

    $out = Join-Path $logDir "$($s.Name).log"
    $err = Join-Path $logDir "$($s.Name).err.log"

    # The launch profile supplies applicationUrl, which is what puts each service
    # on the port the Gateway and the proxy expect. --no-launch-profile skipped
    # both that and ASPNETCORE_ENVIRONMENT.
    $proc = Start-Process -FilePath 'dotnet' `
        -ArgumentList @('run', '--project', $projectPath) `
        -WorkingDirectory $projectPath `
        -RedirectStandardOutput $out -RedirectStandardError $err `
        -WindowStyle Hidden -PassThru

    $startedPids += $proc.Id
    Ok "$($s.Name): started PID $($proc.Id), logging to scripts/logs/$($s.Name).log"
}

if ($startedPids.Count -gt 0) { $startedPids | Set-Content $pidFile }

# ------------------------------------------------------------------------ wait
Step "Waiting for ports (up to ${TimeoutSeconds}s)"
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$pending  = [System.Collections.ArrayList]@($services)

while ($pending.Count -gt 0 -and (Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 3
    foreach ($s in @($pending)) {
        if (Test-Port $s.Port) {
            Ok "$($s.Name) listening on $($s.Port)"
            $pending.Remove($s)
        }
    }
}

foreach ($s in $pending) {
    Bad "$($s.Name) never came up on $($s.Port). Last lines of its log:"
    $log = Join-Path $logDir "$($s.Name).log"
    $errLog = Join-Path $logDir "$($s.Name).err.log"
    foreach ($f in @($log, $errLog)) {
        if ((Test-Path $f) -and (Get-Item $f).Length -gt 0) {
            Write-Host "    --- $(Split-Path $f -Leaf)" -ForegroundColor DarkGray
            Get-Content $f -Tail 30 | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
        }
    }
}

if ($pending.Count -eq 0) { Step 'All services up' } else { Step "$($pending.Count) service(s) failed to start" }
