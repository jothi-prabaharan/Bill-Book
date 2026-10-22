<#
.SYNOPSIS
    Standardized Migration Reset and Baseline Recreation Script for Bill-Book Microservices.
.DESCRIPTION
    1. Removes legacy/scattered migration files across service repositories.
    2. Enforces schema-isolated __EFMigrationsHistory and default schemas.
    3. Generates a fresh, clean 'InitialBaseline' EF Core migration per microservice schema.
.PARAMETER Clean
    If set, removes existing Migrations/ directories before recreating.
.PARAMETER AddBaseline
    If set, executes 'dotnet ef migrations add InitialBaseline' for target services.
.PARAMETER Service
    Optional name of a single service to process (e.g. 'Accounting', 'Master', 'Sales'). Defaults to all services.
.EXAMPLE
    .\scripts\reset-migrations.ps1 -Clean -AddBaseline
    .\scripts\reset-migrations.ps1 -Service Accounting -AddBaseline
#>

[CmdletBinding()]
param(
    [switch]$Clean,
    [switch]$AddBaseline,
    [string]$Service
)

$ErrorActionPreference = "Stop"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "  Bill-Book EF Core Migration Reset & Baseline Recreation Tool   " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

$RepoRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path (Join-Path $RepoRoot "backend"))) {
    $RepoRoot = (Get-Item $PSScriptRoot).Parent.FullName
}

Write-Host "Repository Root: $RepoRoot" -ForegroundColor Gray

# Define all microservices and schema mappings
$Services = @(
    @{ Name = "Accounting"; Schema = "acc"; Context = "AccountingDbContext"; Repo = "backend/Api/Accounting/Accounting.Repository/Accounting.Repository.csproj"; Api = "backend/Api/Accounting/Accounting.Api/Accounting.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "Admission"; Schema = "adm"; Context = "AdmissionDbContext"; Repo = "backend/Api/Admission/Admission.Repository/Admission.Repository.csproj"; Api = "backend/Api/Admission/Admission.Api/Admission.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "Attendance"; Schema = "att"; Context = "AttendanceDbContext"; Repo = "backend/Api/Attendance/Attendance.Repository/Attendance.Repository.csproj"; Api = "backend/Api/Attendance/Attendance.Api/Attendance.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "Claims"; Schema = "clm"; Context = "ClaimsDbContext"; Repo = "backend/Api/Claims/Claims.Repository/Claims.Repository.csproj"; Api = "backend/Api/Claims/Claims.Api/Claims.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "Customer"; Schema = "cus"; Context = "CustomerDbContext"; Repo = "backend/Api/Customer/Customer.Repository/Customer.Repository.csproj"; Api = "backend/Api/Customer/Customer.Api/Customer.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "Employee"; Schema = "hrm"; Context = "EmployeeDbContext"; Repo = "backend/Api/Employee/Employee.Repository/Employee.Repository.csproj"; Api = "backend/Api/Employee/Employee.Api/Employee.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "Facility"; Schema = "fac"; Context = "FacilityDbContext"; Repo = "backend/Api/Facility/Facility.Repository/Facility.Repository.csproj"; Api = "backend/Api/Facility/Facility.Api/Facility.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "Fee"; Schema = "fee"; Context = "FeeDbContext"; Repo = "backend/Api/Fee/Fee.Repository/Fee.Repository.csproj"; Api = "backend/Api/Fee/Fee.Api/Fee.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "Inventory"; Schema = "inv"; Context = "InventoryDbContext"; Repo = "backend/Api/Inventory/Inventory.Repository/Inventory.Repository.csproj"; Api = "backend/Api/Inventory/Inventory.Api/Inventory.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "MaintenanceContract"; Schema = "amc"; Context = "MaintenanceContractDbContext"; Repo = "backend/Api/MaintenanceContract/MaintenanceContract.Repository/MaintenanceContract.Repository.csproj"; Api = "backend/Api/MaintenanceContract/MaintenanceContract.Api/MaintenanceContract.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "Master_Admin"; Schema = "mst"; Context = "AdminDbContext"; Repo = "backend/Api/Master/Master.Repository/Master.Repository.csproj"; Api = "backend/Api/Master/Master.Api/Master.Api.csproj"; OutputDir = "Migrations/Admin" },
    @{ Name = "Master_Contacts"; Schema = "con"; Context = "ContactsDbContext"; Repo = "backend/Api/Master/Master.Repository/Master.Repository.csproj"; Api = "backend/Api/Master/Master.Api/Master.Api.csproj"; OutputDir = "Migrations/Contacts" },
    @{ Name = "Payroll"; Schema = "pay"; Context = "PayrollDbContext"; Repo = "backend/Api/Payroll/Payroll.Repository/Payroll.Repository.csproj"; Api = "backend/Api/Payroll/Payroll.Api/Payroll.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "Performance"; Schema = "prf"; Context = "PerformanceDbContext"; Repo = "backend/Api/Performance/Performance.Repository/Performance.Repository.csproj"; Api = "backend/Api/Performance/Performance.Api/Performance.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "Preventive"; Schema = "ppm"; Context = "PreventiveDbContext"; Repo = "backend/Api/Preventive/Preventive.Repository/Preventive.Repository.csproj"; Api = "backend/Api/Preventive/Preventive.Api/Preventive.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "Printing"; Schema = "prt"; Context = "PrintingDbContext"; Repo = "backend/Api/Printing/Printing.Repository/Printing.Repository.csproj"; Api = "backend/Api/Printing/Printing.Api/Printing.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "Purchase"; Schema = "pur"; Context = "PurchaseDbContext"; Repo = "backend/Api/Purchase/Purchase.Repository/Purchase.Repository.csproj"; Api = "backend/Api/Purchase/Purchase.Api/Purchase.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "Recruitment"; Schema = "rec"; Context = "RecruitmentDbContext"; Repo = "backend/Api/Recruitment/Recruitment.Repository/Recruitment.Repository.csproj"; Api = "backend/Api/Recruitment/Recruitment.Api/Recruitment.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "Reporting"; Schema = "rpt"; Context = "ReportingDbContext"; Repo = "backend/Api/Reporting/Reporting.Repository/Reporting.Repository.csproj"; Api = "backend/Api/Reporting/Reporting.Api/Reporting.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "Sales"; Schema = "sal"; Context = "SalesDbContext"; Repo = "backend/Api/Sales/Sales.Repository/Sales.Repository.csproj"; Api = "backend/Api/Sales/Sales.Api/Sales.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "Site"; Schema = "sit"; Context = "SiteDbContext"; Repo = "backend/Api/Site/Site.Repository/Site.Repository.csproj"; Api = "backend/Api/Site/Site.Api/Site.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "Student"; Schema = "sis"; Context = "StudentDbContext"; Repo = "backend/Api/Student/Student.Repository/Student.Repository.csproj"; Api = "backend/Api/Student/Student.Api/Student.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "TimeLeave"; Schema = "tla"; Context = "TimeLeaveDbContext"; Repo = "backend/Api/TimeLeave/TimeLeave.Repository/TimeLeave.Repository.csproj"; Api = "backend/Api/TimeLeave/TimeLeave.Api/TimeLeave.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "WorkOrder"; Schema = "wrk"; Context = "WorkOrderDbContext"; Repo = "backend/Api/WorkOrder/WorkOrder.Repository/WorkOrder.Repository.csproj"; Api = "backend/Api/WorkOrder/WorkOrder.Api/WorkOrder.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "Gateway"; Schema = "gwy"; Context = "GatewayDbContext"; Repo = "backend/Gateway/Gateway.Repository/Gateway.Repository.csproj"; Api = "backend/Gateway/Gateway.Api/Gateway.Api.csproj"; OutputDir = "Migrations" },
    @{ Name = "Notification"; Schema = "ntf"; Context = "NotificationDbContext"; Repo = "backend/worker/BillBook.Worker/BillBook.Worker.csproj"; Api = "backend/worker/BillBook.Worker/BillBook.Worker.csproj"; OutputDir = "Notification/Migrations" }
)

# Filter by service if specified
if ($Service) {
    $Services = $Services | Where-Object { $_.Name -like "*$Service*" -or $_.Schema -eq $Service }
    if ($Services.Count -eq 0) {
        Write-Error "No matching service found for '$Service'."
    }
}

# 1. Clean legacy migration directories
if ($Clean) {
    Write-Host "`n--- Cleaning Existing Migration Directories ---" -ForegroundColor Yellow
    foreach ($svc in $Services) {
        $repoDir = Split-Path -Parent (Join-Path $RepoRoot $svc.Repo)
        $migrationsPath = Join-Path $repoDir $svc.OutputDir
        if (Test-Path $migrationsPath) {
            Write-Host "  Removing: $migrationsPath" -ForegroundColor DarkGray
            Remove-Item -Path $migrationsPath -Recurse -Force
        }

        # Check and remove any accidental nested 'Repository/Migrations' folders
        $nestedPath = Join-Path $repoDir "Repository/Migrations"
        if (Test-Path $nestedPath) {
            Write-Host "  Removing nested: $nestedPath" -ForegroundColor DarkGray
            Remove-Item -Path $nestedPath -Recurse -Force
        }
    }
    Write-Host "  All target migration directories cleared." -ForegroundColor Green
}

# 2. Add fresh InitialBaseline migrations
if ($AddBaseline) {
    Write-Host "`n--- Generating Fresh InitialBaseline Migrations ---" -ForegroundColor Yellow
    
    # Ensure background build server is reset before starting
    dotnet build-server shutdown | Out-Null

    $successCount = 0
    $failureCount = 0

    foreach ($svc in $Services) {
        $name = $svc.Name
        $context = $svc.Context
        $schema = $svc.Schema
        $repoProj = Join-Path $RepoRoot $svc.Repo
        $apiProj = Join-Path $RepoRoot $svc.Api
        $outDir = $svc.OutputDir

        Write-Host "`n[$name] Schema: '$schema' | Context: '$context'" -ForegroundColor Cyan
        
        $migrationName = "InitialBaseline"
        if ($name.Contains("_")) {
            $migrationName = "InitialBaseline_" + $name.Split("_")[1]
        }

        $cmd = "dotnet ef migrations add $migrationName --context $context --project `"$repoProj`" --startup-project `"$apiProj`" --output-dir `"$outDir`""
        Write-Host "  Executing: $cmd" -ForegroundColor Gray

        try {
            $process = Start-Process -FilePath "dotnet" -ArgumentList "ef", "migrations", "add", $migrationName, "--context", $context, "--project", "`"$repoProj`"", "--startup-project", "`"$apiProj`"", "--output-dir", "`"$outDir`"" -NoNewWindow -PassThru -Wait
            if ($process.ExitCode -eq 0) {
                Write-Host "  SUCCESS: Created $migrationName for $name ($schema)" -ForegroundColor Green
                $successCount++
            } else {
                Write-Host "  FAILED: Migration failed for $name (Exit code $($process.ExitCode))" -ForegroundColor Red
                $failureCount++
            }
        } catch {
            Write-Host "  ERROR: $_" -ForegroundColor Red
            $failureCount++
        }
    }

    Write-Host "`n=================================================================" -ForegroundColor Cyan
    Write-Host "  Summary: $successCount succeeded, $failureCount failed." -ForegroundColor $(if ($failureCount -eq 0) { "Green" } else { "Yellow" })
    Write-Host "=================================================================" -ForegroundColor Cyan
}

if (-not $Clean -and -not $AddBaseline) {
    Write-Host "`nNo actions specified. Pass -Clean to remove existing migrations, and/or -AddBaseline to regenerate." -ForegroundColor Yellow
    Write-Host "Example: .\scripts\reset-migrations.ps1 -Clean -AddBaseline" -ForegroundColor White
}
