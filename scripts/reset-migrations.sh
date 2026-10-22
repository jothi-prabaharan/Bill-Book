#!/usr/bin/env bash
# =================================================================
#  Bill-Book EF Core Migration Reset & Baseline Recreation Tool
# =================================================================
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
echo "Repository Root: $REPO_ROOT"

CLEAN=false
ADD_BASELINE=false
TARGET_SERVICE=""

while [[ $# -gt 0 ]]; do
  case $1 in
    --clean|-c)
      CLEAN=true
      shift
      ;;
    --add-baseline|-b)
      ADD_BASELINE=true
      shift
      ;;
    --service|-s)
      TARGET_SERVICE="$2"
      shift 2
      ;;
    *)
      echo "Unknown option: $1"
      exit 1
      ;;
  esac
done

# Declare service specifications: Name|Schema|Context|RepoPath|ApiPath|OutputDir
SERVICES=(
  "Accounting|acc|AccountingDbContext|backend/Api/Accounting/Accounting.Repository/Accounting.Repository.csproj|backend/Api/Accounting/Accounting.Api/Accounting.Api.csproj|Migrations"
  "Admission|adm|AdmissionDbContext|backend/Api/Admission/Admission.Repository/Admission.Repository.csproj|backend/Api/Admission/Admission.Api/Admission.Api.csproj|Migrations"
  "Attendance|att|AttendanceDbContext|backend/Api/Attendance/Attendance.Repository/Attendance.Repository.csproj|backend/Api/Attendance/Attendance.Api/Attendance.Api.csproj|Migrations"
  "Claims|clm|ClaimsDbContext|backend/Api/Claims/Claims.Repository/Claims.Repository.csproj|backend/Api/Claims/Claims.Api/Claims.Api.csproj|Migrations"
  "Customer|cus|CustomerDbContext|backend/Api/Customer/Customer.Repository/Customer.Repository.csproj|backend/Api/Customer/Customer.Api/Customer.Api.csproj|Migrations"
  "Employee|hrm|EmployeeDbContext|backend/Api/Employee/Employee.Repository/Employee.Repository.csproj|backend/Api/Employee/Employee.Api/Employee.Api.csproj|Migrations"
  "Facility|fac|FacilityDbContext|backend/Api/Facility/Facility.Repository/Facility.Repository.csproj|backend/Api/Facility/Facility.Api/Facility.Api.csproj|Migrations"
  "Fee|fee|FeeDbContext|backend/Api/Fee/Fee.Repository/Fee.Repository.csproj|backend/Api/Fee/Fee.Api/Fee.Api.csproj|Migrations"
  "Inventory|inv|InventoryDbContext|backend/Api/Inventory/Inventory.Repository/Inventory.Repository.csproj|backend/Api/Inventory/Inventory.Api/Inventory.Api.csproj|Migrations"
  "MaintenanceContract|amc|MaintenanceContractDbContext|backend/Api/MaintenanceContract/MaintenanceContract.Repository/MaintenanceContract.Repository.csproj|backend/Api/MaintenanceContract/MaintenanceContract.Api/MaintenanceContract.Api.csproj|Migrations"
  "Master_Admin|mst|AdminDbContext|backend/Api/Master/Master.Repository/Master.Repository.csproj|backend/Api/Master/Master.Api/Master.Api.csproj|Migrations/Admin"
  "Master_Contacts|con|ContactsDbContext|backend/Api/Master/Master.Repository/Master.Repository.csproj|backend/Api/Master/Master.Api/Master.Api.csproj|Migrations/Contacts"
  "Payroll|pay|PayrollDbContext|backend/Api/Payroll/Payroll.Repository/Payroll.Repository.csproj|backend/Api/Payroll/Payroll.Api/Payroll.Api.csproj|Migrations"
  "Performance|prf|PerformanceDbContext|backend/Api/Performance/Performance.Repository/Performance.Repository.csproj|backend/Api/Performance/Performance.Api/Performance.Api.csproj|Migrations"
  "Preventive|ppm|PreventiveDbContext|backend/Api/Preventive/Preventive.Repository/Preventive.Repository.csproj|backend/Api/Preventive/Preventive.Api/Preventive.Api.csproj|Migrations"
  "Printing|prt|PrintingDbContext|backend/Api/Printing/Printing.Repository/Printing.Repository.csproj|backend/Api/Printing/Printing.Api/Printing.Api.csproj|Migrations"
  "Purchase|pur|PurchaseDbContext|backend/Api/Purchase/Purchase.Repository/Purchase.Repository.csproj|backend/Api/Purchase/Purchase.Api/Purchase.Api.csproj|Migrations"
  "Recruitment|rec|RecruitmentDbContext|backend/Api/Recruitment/Recruitment.Repository/Recruitment.Repository.csproj|backend/Api/Recruitment/Recruitment.Api/Recruitment.Api.csproj|Migrations"
  "Reporting|rpt|ReportingDbContext|backend/Api/Reporting/Reporting.Repository/Reporting.Repository.csproj|backend/Api/Reporting/Reporting.Api/Reporting.Api.csproj|Migrations"
  "Sales|sal|SalesDbContext|backend/Api/Sales/Sales.Repository/Sales.Repository.csproj|backend/Api/Sales/Sales.Api/Sales.Api.csproj|Migrations"
  "Site|sit|SiteDbContext|backend/Api/Site/Site.Repository/Site.Repository.csproj|backend/Api/Site/Site.Api/Site.Api.csproj|Migrations"
  "Student|sis|StudentDbContext|backend/Api/Student/Student.Repository/Student.Repository.csproj|backend/Api/Student/Student.Api/Student.Api.csproj|Migrations"
  "TimeLeave|tla|TimeLeaveDbContext|backend/Api/TimeLeave/TimeLeave.Repository/TimeLeave.Repository.csproj|backend/Api/TimeLeave/TimeLeave.Api/TimeLeave.Api.csproj|Migrations"
  "WorkOrder|wrk|WorkOrderDbContext|backend/Api/WorkOrder/WorkOrder.Repository/WorkOrder.Repository.csproj|backend/Api/WorkOrder/WorkOrder.Api/WorkOrder.Api.csproj|Migrations"
  "Gateway|gwy|GatewayDbContext|backend/Gateway/Gateway.Repository/Gateway.Repository.csproj|backend/Gateway/Gateway.Api/Gateway.Api.csproj|Migrations"
  "Notification|ntf|NotificationDbContext|backend/worker/BillBook.Worker/BillBook.Worker.csproj|backend/worker/BillBook.Worker/BillBook.Worker.csproj|Notification/Migrations"
)

if [ "$CLEAN" = true ]; then
  echo "--- Cleaning Existing Migration Directories ---"
  for entry in "${SERVICES[@]}"; do
    IFS="|" read -r NAME SCHEMA CONTEXT REPO API OUTDIR <<< "$entry"
    if [ -n "$TARGET_SERVICE" ] && [[ "$NAME" != *"$TARGET_SERVICE"* ]]; then
      continue
    fi
    REPO_DIR="$(dirname "$REPO_ROOT/$REPO")"
    MIGRATIONS_PATH="$REPO_DIR/$OUTDIR"
    if [ -d "$MIGRATIONS_PATH" ]; then
      echo "  Removing: $MIGRATIONS_PATH"
      rm -rf "$MIGRATIONS_PATH"
    fi
    NESTED_PATH="$REPO_DIR/Repository/Migrations"
    if [ -d "$NESTED_PATH" ]; then
      echo "  Removing nested: $NESTED_PATH"
      rm -rf "$NESTED_PATH"
    fi
  done
  echo "  Done cleaning."
fi

if [ "$ADD_BASELINE" = true ]; then
  echo "--- Generating Fresh InitialBaseline Migrations ---"
  dotnet build-server shutdown || true

  for entry in "${SERVICES[@]}"; do
    IFS="|" read -r NAME SCHEMA CONTEXT REPO API OUTDIR <<< "$entry"
    if [ -n "$TARGET_SERVICE" ] && [[ "$NAME" != *"$TARGET_SERVICE"* ]]; then
      continue
    fi
    echo "[$NAME] Schema: $SCHEMA | Context: $CONTEXT"
    MIGRATION_NAME="InitialBaseline"
    if [[ "$NAME" == *"_"* ]]; then
      MIGRATION_NAME="InitialBaseline_${NAME#*_}"
    fi

    dotnet ef migrations add "$MIGRATION_NAME" \
      --context "$CONTEXT" \
      --project "$REPO_ROOT/$REPO" \
      --startup-project "$REPO_ROOT/$API" \
      --output-dir "$OUTDIR"
    echo "  SUCCESS: Created $MIGRATION_NAME for $NAME"
  done
fi
