#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEPLOY_BASE="${DEPLOY_DIR:-/home/prabha/deploy/Stating}"
DIST_DIR="$DEPLOY_BASE/dist"
LOG_DIR="$DEPLOY_BASE/logs"
RUN_DIR="$DEPLOY_BASE/run"

mkdir -p "$LOG_DIR" "$RUN_DIR"

export ASPNETCORE_ENVIRONMENT=Staging
export ConnectionStrings__AdminDatabase="Host=192.168.1.101;Port=5100;Database=EP_Admin;Username=postgres;Password=555"
export ConnectionStrings__TenantDatabase="Host=192.168.1.101;Port=5100;Database=IN000001;Username=postgres;Password=555"

export Master__BaseUrl="http://localhost:5504"
export Accounting__BaseUrl="http://localhost:5501"
export Customer__BaseUrl="http://localhost:5502"
export Inventory__BaseUrl="http://localhost:5503"
export Purchase__BaseUrl="http://localhost:5505"
export Reporting__BaseUrl="http://localhost:5506"
export Sales__BaseUrl="http://localhost:5507"
export Printing__BaseUrl="http://localhost:5508"
export Employee__BaseUrl="http://localhost:5509"
export TimeLeave__BaseUrl="http://localhost:5510"
export Payroll__BaseUrl="http://localhost:5511"
export Recruitment__BaseUrl="http://localhost:5512"
export Performance__BaseUrl="http://localhost:5513"
export Claims__BaseUrl="http://localhost:5514"
export Student__BaseUrl="http://localhost:5515"
export Admission__BaseUrl="http://localhost:5516"
export Attendance__BaseUrl="http://localhost:5517"
export Fee__BaseUrl="http://localhost:5518"
export Facility__BaseUrl="http://localhost:5519"
export WorkOrder__BaseUrl="http://localhost:5520"
export Preventive__BaseUrl="http://localhost:5521"
export MaintenanceContract__BaseUrl="http://localhost:5522"
export Internal__ApiKey="staging-internal-key-secure-2026-xyz"
export Jwt__SigningKey="staging-signing-key-updated-secure-32-bytes-min-value-2026-xyz"


echo "=========================================================="
echo " Starting Bill-Book Staging Services"
echo " Environment: Staging | Database: 192.168.1.101:5100 (EP_Admin)"
echo "=========================================================="

start_daemon() {
  local name="$1"
  local bin_path="$2"
  local port="$3"
  local pid_file="$RUN_DIR/$name.pid"
  local log_file="$LOG_DIR/$name.log"

  if [ -f "$pid_file" ] && kill -0 "$(cat "$pid_file")" 2>/dev/null; then
    echo "  [ALREADY RUNNING] $name (PID $(cat "$pid_file"), Port ${port:--})"
    return 0
  fi

  if [ ! -f "$bin_path" ]; then
    echo "  [ERROR] Binary for $name not found at $bin_path"
    return 1
  fi

  local bin_dir
  bin_dir="$(dirname "$bin_path")"
  local bin_file
  bin_file="$(basename "$bin_path")"

  if [ -n "$port" ]; then
    (
      cd "$bin_dir"
      ASPNETCORE_URLS="http://0.0.0.0:$port" \
      nohup dotnet "$bin_file" </dev/null > "$log_file" 2>&1 &
      echo $! > "$pid_file"
    )
  else
    (
      cd "$bin_dir"
      nohup dotnet "$bin_file" </dev/null > "$log_file" 2>&1 &
      echo $! > "$pid_file"
    )
  fi

  local new_pid
  new_pid="$(cat "$pid_file")"
  echo "  [STARTED] $name (PID $new_pid, Port ${port:--})"
}

# 1. Start Master Service First (Runs Master & Tenant Migrations)
echo "--- Starting Master API (Database & Schema Initializer) ---"
start_daemon "master" "$DIST_DIR/backend/master/Master.Api.dll" "5504"
echo "  Waiting 5 seconds for Master migrations..."
sleep 5

# 2. Start Remaining Microservices
echo "--- Starting Domain Microservices ---"
MICROSERVICES=(
  "accounting:backend/accounting/Accounting.Api.dll:5501"
  "customer:backend/customer/Customer.Api.dll:5502"
  "inventory:backend/inventory/Inventory.Api.dll:5503"
  "purchase:backend/purchase/Purchase.Api.dll:5505"
  "reporting:backend/reporting/Reporting.Api.dll:5506"
  "sales:backend/sales/Sales.Api.dll:5507"
  "printing:backend/printing/Printing.Api.dll:5508"
  "employee:backend/employee/Employee.Api.dll:5509"
  "timeleave:backend/timeleave/TimeLeave.Api.dll:5510"
  "payroll:backend/payroll/Payroll.Api.dll:5511"
  "recruitment:backend/recruitment/Recruitment.Api.dll:5512"
  "performance:backend/performance/Performance.Api.dll:5513"
  "claims:backend/claims/Claims.Api.dll:5514"
  "student:backend/student/Student.Api.dll:5515"
  "admission:backend/admission/Admission.Api.dll:5516"
  "attendance:backend/attendance/Attendance.Api.dll:5517"
  "fee:backend/fee/Fee.Api.dll:5518"
  "facility:backend/facility/Facility.Api.dll:5519"
  "workorder:backend/workorder/WorkOrder.Api.dll:5520"
  "preventive:backend/preventive/Preventive.Api.dll:5521"
  "amc:backend/maintenancecontract/MaintenanceContract.Api.dll:5522"
)

for item in "${MICROSERVICES[@]}"; do
  IFS=":" read -r name rel_path port <<< "$item"
  start_daemon "$name" "$DIST_DIR/$rel_path" "$port"
done

# 3. Start Gateway
echo "--- Starting Gateway ---"
start_daemon "gateway" "$DIST_DIR/backend/gateway/Gateway.Api.dll" "5500"

# 4. Start Background Worker
echo "--- Starting Background Worker ---"
start_daemon "worker" "$DIST_DIR/backend/worker/BillBook.Worker.dll" ""

# 5. Start Frontend Static Server
echo "--- Starting Frontend SPA ---"
FRONTEND_PID="$RUN_DIR/frontend.pid"
FRONTEND_LOG="$LOG_DIR/frontend.log"

if [ -f "$FRONTEND_PID" ] && kill -0 "$(cat "$FRONTEND_PID")" 2>/dev/null; then
  echo "  [ALREADY RUNNING] Frontend SPA (PID $(cat "$FRONTEND_PID"), Port 5200)"
else
  (
    cd "$SCRIPT_DIR"
    PORT=5200 nohup node serve-frontend.js </dev/null > "$FRONTEND_LOG" 2>&1 &
    echo $! > "$FRONTEND_PID"
  )
  echo "  [STARTED] Frontend SPA (PID $(cat "$FRONTEND_PID"), Port 5200)"
fi

echo "=========================================================="
echo " All services launched. Waiting 4 seconds for port binding..."
sleep 4
"$SCRIPT_DIR/status.sh"
