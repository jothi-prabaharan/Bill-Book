#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
DEPLOY_BASE="${DEPLOY_DIR:-/home/prabha/deploy/Stating}"
DIST_DIR="$DEPLOY_BASE/dist"
BRANCH="${1:-Stating}"

echo "=========================================================="
echo " Starting One-Click Deployment for STAGING"
echo " Time: $(date)"
echo " Repo: $REPO_ROOT"
echo " Target Branch: $BRANCH"
echo " Deployment Files Path: $DEPLOY_BASE"
echo "=========================================================="

cd "$REPO_ROOT"

# -----------------------------------------------------------------------------
# 1. GIT CHECKOUT & PULL LATEST CODE BEFORE DEPLOYMENT
# -----------------------------------------------------------------------------
echo -e "\n[STEP 1/5] Fetching and Pulling Latest Code from '$BRANCH'..."
if git rev-parse --verify "$BRANCH" >/dev/null 2>&1; then
    echo "  Checking out '$BRANCH'..."
    git checkout "$BRANCH"
else
    echo "  Creating local branch '$BRANCH'..."
    git checkout -b "$BRANCH"
fi

echo "  Fetching latest commits from origin..."
git fetch origin "$BRANCH" 2>/dev/null || git fetch origin 2>/dev/null || true

echo "  Pulling latest code into '$BRANCH'..."
git pull origin "$BRANCH" 2>/dev/null || echo "  [INFO] Already up to date or local branch synchronized."


# -----------------------------------------------------------------------------
# 2. STAGING DATABASE SETUP (POSTGRESQL PORT 5100)
# -----------------------------------------------------------------------------
echo -e "\n[STEP 2/5] Preparing Staging Database on PostgreSQL Port 5100..."
PG_HOST="192.168.1.101"
PG_PORT="5100"
PG_USER="postgres"
export PGPASSWORD="555"

if command -v psql >/dev/null 2>&1; then
    if PGPASSWORD="$PGPASSWORD" psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -c "SELECT 1;" >/dev/null 2>&1; then
        echo "  [OK] Connected to PostgreSQL cluster at $PG_HOST:$PG_PORT"
        
        # Check / Create EP_Admin
        DB_EXISTS=$(PGPASSWORD="$PGPASSWORD" psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -t -A -c "SELECT 1 FROM pg_database WHERE datname = 'EP_Admin';")
        if [ "$DB_EXISTS" != "1" ]; then
            echo "  Creating 'EP_Admin' database..."
            PGPASSWORD="$PGPASSWORD" psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -c 'CREATE DATABASE "EP_Admin" WITH OWNER postgres ENCODING "UTF8";'
            echo "  [OK] Created database 'EP_Admin'"
        else
            echo "  [OK] Database 'EP_Admin' already exists."
        fi

        # Check / Create IN000001 (Template/Shard tenant)
        TENANT_EXISTS=$(PGPASSWORD="$PGPASSWORD" psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -t -A -c "SELECT 1 FROM pg_database WHERE datname = 'IN000001';")
        if [ "$TENANT_EXISTS" != "1" ]; then
            echo "  Creating 'IN000001' tenant database..."
            PGPASSWORD="$PGPASSWORD" psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -c 'CREATE DATABASE "IN000001" WITH OWNER postgres ENCODING "UTF8";'
            echo "  [OK] Created database 'IN000001'"
        else
            echo "  [OK] Database 'IN000001' already exists."
        fi
    else
        echo "  [WARN] Could not connect to PostgreSQL at $PG_HOST:$PG_PORT with user $PG_USER."
        echo "         Please ensure cluster 18/stg is running."
    fi
else
    echo "  [WARN] psql command not found, skipping database pre-check."
fi

# -----------------------------------------------------------------------------
# 3. BUILD BACKEND SERVICES (Release Mode, Staging Environment)
# -----------------------------------------------------------------------------
echo -e "\n[STEP 3/5] Publishing Backend Microservices and Gateway (.NET 10)..."
mkdir -p "$DIST_DIR/backend"

BACKEND_PROJECTS=(
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

for proj in "${BACKEND_PROJECTS[@]}"; do
  IFS=":" read -r name rel_path <<< "$proj"
  out_path="$DIST_DIR/backend/$name"
  echo "  Publishing $name..."
  dotnet publish "$rel_path" -c Release -p:EnvironmentName=Staging -o "$out_path" --nologo -v q
done

echo "  [OK] All backend services published successfully."

# -----------------------------------------------------------------------------
# 4. BUILD FRONTEND APPLICATION (Angular 20 / Nx)
# -----------------------------------------------------------------------------
echo -e "\n[STEP 4/5] Building Angular Web Application for Staging..."
mkdir -p "$DIST_DIR/frontend/web"

cd "$REPO_ROOT/frontend"
if [ ! -d "node_modules" ]; then
    echo "  Frontend node_modules not found. Installing packages..."
    npm install --legacy-peer-deps
fi
npx --yes nx build web -c staging --base-href "/"

if [ -d "$REPO_ROOT/frontend/dist/apps/web/browser" ]; then
    cp -r "$REPO_ROOT/frontend/dist/apps/web/browser"/* "$DIST_DIR/frontend/web/"
elif [ -d "$REPO_ROOT/frontend/dist/apps/web" ]; then
    cp -r "$REPO_ROOT/frontend/dist/apps/web"/* "$DIST_DIR/frontend/web/"
fi

echo "  [OK] Frontend build complete and copied to dist/frontend/web."

# -----------------------------------------------------------------------------
# 5. RESTART AND HEALTH CHECK
# -----------------------------------------------------------------------------
echo -e "\n[STEP 5/5] Restarting Staging Services..."
"$SCRIPT_DIR/stop.sh" || true
sleep 2
"$SCRIPT_DIR/start.sh"

echo "=========================================================="
echo " STAGING DEPLOYMENT COMPLETED SUCCESSFULLY!"
echo " Public Access Endpoints:"
echo "   - Web App UI:       http://localhost:5200"
echo "   - API Gateway:      http://localhost:5500"
echo "   - Swagger Docs:     http://localhost:5500/swagger"
echo "   - Staging DB:       $PG_HOST:$PG_PORT/EP_Admin"
echo "=========================================================="
