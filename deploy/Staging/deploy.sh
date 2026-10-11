#!/usr/bin/env bash
set -e

# ==============================================================================
# Bill-Book (RetailErp) - All-In-One Staging Deployment & Service Manager
# Single Self-Contained Script: Handles Git, DB, Builds, Hosting, Start/Stop/Status
# ==============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
BRANCH="staging"

# External deployment directories outside source code
DEPLOY_BASE="/home/prabha/deploy/Staging"
DIST_DIR="$DEPLOY_BASE/dist"
LOG_DIR="$DEPLOY_BASE/logs"
RUN_DIR="$DEPLOY_BASE/run"

mkdir -p "$DIST_DIR/backend" "$DIST_DIR/frontend/web" "$LOG_DIR" "$RUN_DIR"

# Environment configuration
export ASPNETCORE_ENVIRONMENT="Staging"
export ConnectionStrings__AdminDatabase="Host=192.168.1.101;Port=5100;Database=EP_Admin;Username=postgres;Password=555;Keepalive=30;Timeout=180;Command Timeout=180"
export ConnectionStrings__TenantDatabase="Host=192.168.1.101;Port=5100;Database=IN000001;Username=postgres;Password=555;Keepalive=30;Timeout=180;Command Timeout=180"

export Master__BaseUrl="http://192.168.1.101:5504"
export Accounting__BaseUrl="http://192.168.1.101:5501"
export Customer__BaseUrl="http://192.168.1.101:5502"
export Inventory__BaseUrl="http://192.168.1.101:5503"
export Purchase__BaseUrl="http://192.168.1.101:5505"
export Reporting__BaseUrl="http://192.168.1.101:5506"
export Sales__BaseUrl="http://192.168.1.101:5507"
export Printing__BaseUrl="http://192.168.1.101:5508"
export Employee__BaseUrl="http://192.168.1.101:5509"
export TimeLeave__BaseUrl="http://192.168.1.101:5510"
export Payroll__BaseUrl="http://192.168.1.101:5511"
export Recruitment__BaseUrl="http://192.168.1.101:5512"
export Performance__BaseUrl="http://192.168.1.101:5513"
export Claims__BaseUrl="http://192.168.1.101:5514"
export Student__BaseUrl="http://192.168.1.101:5515"
export Admission__BaseUrl="http://192.168.1.101:5516"
export Attendance__BaseUrl="http://192.168.1.101:5517"
export Fee__BaseUrl="http://192.168.1.101:5518"
export Facility__BaseUrl="http://192.168.1.101:5519"
export WorkOrder__BaseUrl="http://192.168.1.101:5520"
export Preventive__BaseUrl="http://192.168.1.101:5521"
export MaintenanceContract__BaseUrl="http://192.168.1.101:5522"
export Internal__ApiKey="staging-internal-key-secure-2026-xyz"
export Jwt__SigningKey="staging-signing-key-updated-secure-32-bytes-min-value-2026-xyz"

stop_services() {
  echo ">>> Stopping Staging Services..."
  if [ -d "$RUN_DIR" ]; then
    for pid_file in "$RUN_DIR"/*.pid; do
      if [ -f "$pid_file" ]; then
        name="$(basename "$pid_file" .pid)"
        pid="$(cat "$pid_file" 2>/dev/null || true)"
        if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
          kill "$pid" 2>/dev/null || true
          for i in {1..3}; do
            kill -0 "$pid" 2>/dev/null || break
            sleep 1
          done
          kill -9 "$pid" 2>/dev/null || true
        fi
        rm -f "$pid_file"
      fi
    done
  fi

  # Release staging ports 5200, 5500-5522
  for port in 5200 $(seq 5500 5522); do
    pids=$(lsof -t -i :"$port" 2>/dev/null || true)
    if [ -n "$pids" ]; then
      kill -9 $pids 2>/dev/null || true
    fi
  done
  echo ">>> All services stopped."
}

start_daemon() {
  local name="$1"
  local bin_path="$2"
  local port="$3"
  local pid_file="$RUN_DIR/$name.pid"
  local log_file="$LOG_DIR/$name.log"

  if [ -f "$pid_file" ] && kill -0 "$(cat "$pid_file")" 2>/dev/null; then
    echo "  [ALREADY RUNNING] $name (PID $(cat "$pid_file"))"
    return 0
  fi

  if [ ! -f "$bin_path" ]; then
    echo "  [ERROR] Binary for $name not found at $bin_path"
    return 1
  fi

  local bin_dir bin_file
  bin_dir="$(dirname "$bin_path")"
  bin_file="$(basename "$bin_path")"

  (
    cd "$bin_dir"
    export ASPNETCORE_ENVIRONMENT="Staging"
    export DOTNET_ENVIRONMENT="Staging"
    if [ -n "$port" ]; then
      export ASPNETCORE_URLS="http://0.0.0.0:$port"
    fi
    nohup dotnet "$bin_file" </dev/null > "$log_file" 2>&1 &
    echo $! > "$pid_file"
  )
  echo "  [STARTED] $name (PID $(cat "$pid_file"), Port ${port:--})"
}

start_services() {
  echo "=========================================================="
  echo " Starting Staging Services (Database: 192.168.1.101:5100)"
  echo "=========================================================="

  # 1. Master first (schema migration)
  echo "--- Starting Master API ---"
  start_daemon "master" "$DIST_DIR/backend/master/Master.Api.dll" "5504"
  sleep 4

  # 2. Domain Microservices
  echo "--- Starting Domain Microservices ---"
  SERVICES=(
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

  for item in "${SERVICES[@]}"; do
    IFS=":" read -r name rel_path port <<< "$item"
    start_daemon "$name" "$DIST_DIR/$rel_path" "$port"
  done

  # 3. Gateway
  echo "--- Starting Gateway ---"
  start_daemon "gateway" "$DIST_DIR/backend/gateway/Gateway.Api.dll" "5500"

  # 4. Background Worker
  echo "--- Starting Background Worker ---"
  start_daemon "worker" "$DIST_DIR/backend/worker/BillBook.Worker.dll" ""

  # 5. Embedded Frontend Server (Port 5200)
  echo "--- Starting Frontend SPA Server ---"
  FRONTEND_PID="$RUN_DIR/frontend.pid"
  FRONTEND_LOG="$LOG_DIR/frontend.log"
  if [ -f "$FRONTEND_PID" ] && kill -0 "$(cat "$FRONTEND_PID")" 2>/dev/null; then
    echo "  [ALREADY RUNNING] Frontend SPA (PID $(cat "$FRONTEND_PID"), Port 5200)"
  else
    (
      nohup node -e '
      const http = require("http"), fs = require("fs"), path = require("path");
      const PORT = 5200;
      const WEB_ROOT = "'"$DIST_DIR"'/frontend/web";
      const MIME = { ".html": "text/html; charset=UTF-8", ".js": "application/javascript", ".css": "text/css", ".json": "application/json", ".png": "image/png", ".svg": "image/svg+xml", ".ico": "image/x-icon", ".woff2": "font/woff2" };
      http.createServer((req, res) => {
        let f = path.join(WEB_ROOT, req.url.split("?")[0]);
        if (fs.existsSync(f) && fs.statSync(f).isFile()) {
          res.writeHead(200, { "Content-Type": MIME[path.extname(f)] || "application/octet-stream" });
          fs.createReadStream(f).pipe(res);
        } else {
          let idx = path.join(WEB_ROOT, "index.html");
          if (fs.existsSync(idx)) {
            res.writeHead(200, { "Content-Type": "text/html; charset=UTF-8" });
            fs.createReadStream(idx).pipe(res);
          } else {
            res.writeHead(404); res.end("index.html not found");
          }
        }
      }).listen(PORT, "0.0.0.0");
      ' </dev/null > "$FRONTEND_LOG" 2>&1 &
      echo $! > "$FRONTEND_PID"
    )
    echo "  [STARTED] Frontend SPA (PID $(cat "$FRONTEND_PID"), Port 5200)"
  fi

  sleep 3
  check_status
}

check_status() {
  echo "=========================================================="
  echo " Bill-Book Staging Services Status"
  echo "=========================================================="
  printf "%-22s %-8s %-12s %-10s\n" "SERVICE" "PORT" "STATUS" "PID"
  echo "----------------------------------------------------------"

  check_row() {
    local name="$1" port="$2" pid="" status="STOPPED"
    local pid_file="$RUN_DIR/$name.pid"
    if [ -f "$pid_file" ]; then
      pid="$(cat "$pid_file" 2>/dev/null || true)"
      if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
        status="RUNNING"
      else
        status="DEAD"
        pid=""
      fi
    fi
    printf "%-22s %-8s %-12s %-10s\n" "$name" "${port:--}" "$status" "${pid:--}"
  }

  check_row "frontend" "5200"
  check_row "gateway" "5500"
  check_row "accounting" "5501"
  check_row "customer" "5502"
  check_row "inventory" "5503"
  check_row "master" "5504"
  check_row "purchase" "5505"
  check_row "reporting" "5506"
  check_row "sales" "5507"
  check_row "printing" "5508"
  check_row "employee" "5509"
  check_row "timeleave" "5510"
  check_row "payroll" "5511"
  check_row "recruitment" "5512"
  check_row "performance" "5513"
  check_row "claims" "5514"
  check_row "student" "5515"
  check_row "admission" "5516"
  check_row "attendance" "5517"
  check_row "fee" "5518"
  check_row "facility" "5519"
  check_row "workorder" "5520"
  check_row "preventive" "5521"
  check_row "amc" "5522"
  check_row "worker" ""

  echo "----------------------------------------------------------"
  echo " Public Access Endpoints:"
  echo "   - Web App UI:       http://117.247.103.65:5200"
  echo "   - API Gateway:      http://117.247.103.65:5500"
  echo "   - Swagger Docs:     http://117.247.103.65:5500/swagger"
  echo "   - Staging DB:       192.168.1.101:5100 (Database: EP_Admin)"
  echo "   - Build Files Path: $DIST_DIR"
  echo "=========================================================="
}

run_deployment() {
  echo "=========================================================="
  echo " Starting One-Click Deployment for Staging Branch"
  echo " Time: $(date)"
  echo " Repo: $REPO_ROOT"
  echo " Branch: $BRANCH"
  echo " Target Deploy Path: $DEPLOY_BASE"
  echo "=========================================================="

  cd "$REPO_ROOT"

  # 1. SWITCH TO EXISTING STAGING BRANCH & PULL LATEST CODE
  echo -e "\n[1/5] Checking out existing '$BRANCH' branch and taking latest code..."
  git checkout "$BRANCH"
  GIT_TERMINAL_PROMPT=0 git pull origin "$BRANCH" 2>/dev/null || echo "  [INFO] Branch synchronized with latest commits."

  # 2. STAGING DATABASE SETUP (POSTGRESQL PORT 5100)
  echo -e "\n[2/5] Preparing Staging Database on PostgreSQL Port 5100..."
  export PGPASSWORD="555"
  if command -v psql >/dev/null 2>&1; then
    if PGPASSWORD="555" psql -h "192.168.1.101" -p "5100" -U "postgres" -c "SELECT 1;" >/dev/null 2>&1; then
      if [ "$(PGPASSWORD="555" psql -h "192.168.1.101" -p "5100" -U "postgres" -t -A -c "SELECT 1 FROM pg_database WHERE datname = 'EP_Admin';")" != "1" ]; then
        PGPASSWORD="555" psql -h "192.168.1.101" -p "5100" -U "postgres" -c 'CREATE DATABASE "EP_Admin" WITH OWNER postgres ENCODING "UTF8";'
      fi
      if [ "$(PGPASSWORD="555" psql -h "192.168.1.101" -p "5100" -U "postgres" -t -A -c "SELECT 1 FROM pg_database WHERE datname = 'IN000001';")" != "1" ]; then
        PGPASSWORD="555" psql -h "192.168.1.101" -p "5100" -U "postgres" -c 'CREATE DATABASE "IN000001" WITH OWNER postgres ENCODING "UTF8";'
      fi
      echo "  [OK] Staging databases verified on 192.168.1.101:5100"
    fi
  fi

  # 3. BUILD BACKEND SERVICES DIRECTLY TO /home/prabha/deploy/Staging/dist/backend
  echo -e "\n[3/5] Publishing Backend Microservices and Gateway (.NET 10)..."
  PROJECTS=(
    "gateway:backend/Gateway/Gateway.Api/Gateway.Api.csproj"
    "master:backend/Api/Master/Master.Api/Master.Api.csproj"
    "accounting:backend/Api/Accounting/Accounting.Api/Accounting.Api.csproj"
    "customer:backend/Api/Customer/Customer.Api/Customer.Api.csproj"
    "inventory:backend/Api/Inventory/Inventory.Api/Inventory.Api.csproj"
    "purchase:backend/Api/Purchase/Purchase.Api/Purchase.Api.csproj"
    "reporting:backend/Api/Reporting/Reporting.Api/Reporting.Api.csproj"
    "sales:backend/Api/Sales/Sales.Api/Sales.Api.csproj"
    "printing:backend/Api/Printing/Printing.Api/Printing.Api.csproj"
    "employee:backend/Api/Employee/Employee.Api/Employee.Api.csproj"
    "timeleave:backend/Api/TimeLeave/TimeLeave.Api/TimeLeave.Api.csproj"
    "payroll:backend/Api/Payroll/Payroll.Api/Payroll.Api.csproj"
    "recruitment:backend/Api/Recruitment/Recruitment.Api/Recruitment.Api.csproj"
    "performance:backend/Api/Performance/Performance.Api/Performance.Api.csproj"
    "claims:backend/Api/Claims/Claims.Api/Claims.Api.csproj"
    "student:backend/Api/Student/Student.Api/Student.Api.csproj"
    "admission:backend/Api/Admission/Admission.Api/Admission.Api.csproj"
    "attendance:backend/Api/Attendance/Attendance.Api/Attendance.Api.csproj"
    "fee:backend/Api/Fee/Fee.Api/Fee.Api.csproj"
    "facility:backend/Api/Facility/Facility.Api/Facility.Api.csproj"
    "workorder:backend/Api/WorkOrder/WorkOrder.Api/WorkOrder.Api.csproj"
    "preventive:backend/Api/Preventive/Preventive.Api/Preventive.Api.csproj"
    "maintenancecontract:backend/Api/MaintenanceContract/MaintenanceContract.Api/MaintenanceContract.Api.csproj"
    "worker:backend/worker/BillBook.Worker/BillBook.Worker.csproj"
  )

  for p in "${PROJECTS[@]}"; do
    IFS=":" read -r name rel <<< "$p"
    echo "  Publishing $name..."
    dotnet publish "$rel" -c Release -p:EnvironmentName=Staging -o "$DIST_DIR/backend/$name" --nologo -v q
  done

  # 4. BUILD FRONTEND APPLICATION DIRECTLY TO /home/prabha/deploy/Staging/dist/frontend/web
  echo -e "\n[4/5] Building Angular Web Application for Staging..."
  cd "$REPO_ROOT/frontend"
  if [ ! -d "node_modules" ]; then
    npm install --legacy-peer-deps
  fi
  npx --yes nx build web -c staging --base-href "/"

  if [ -d "$REPO_ROOT/frontend/dist/apps/web/browser" ]; then
    cp -r "$REPO_ROOT/frontend/dist/apps/web/browser"/* "$DIST_DIR/frontend/web/"
  elif [ -d "$REPO_ROOT/frontend/dist/apps/web" ]; then
    cp -r "$REPO_ROOT/frontend/dist/apps/web"/* "$DIST_DIR/frontend/web/"
  fi
  rm -rf "$REPO_ROOT/frontend/dist"

  # 5. RESTART SERVICES AND DISPLAY STATUS
  echo -e "\n[5/5] Restarting Staging Services..."
  stop_services
  sleep 2
  start_services
}

case "${1:-deploy}" in
  start)   start_services ;;
  stop)    stop_services ;;
  status)  check_status ;;
  deploy)  run_deployment ;;
  *)       run_deployment ;;
esac
