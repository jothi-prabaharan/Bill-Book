#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# Bill-Book (RetailErp) - Production deployment and service manager
#
#   ./deploy.sh [deploy|start|stop|status]      (default: deploy)
#
# Reads the 'production' branch (fast-forward only), publishes every service, builds
# the web app with the 'production' configuration and restarts everything. It never
# commits, never switches the branch's contents by hand, and never writes to git.
#
# Ports   gateway 7500, web app 7200, APIs 7501 - 7523 (service offset 01-22; 23 is Site)
# Public  http://117.247.103.65:<port>      Internal  http://192.168.1.101:<port>
#
# Secrets are NOT in this file or in the repository. Put them in
#   $DEPLOY_BASE/secrets.env   (chmod 600), as plain KEY=value lines:
#     DB_PASSWORD=...  INTERNAL_API_KEY=...  JWT_SIGNING_KEY=...  (JWT key: 32+ bytes)
#   optional: BOOTSTRAP_OWNER_EMAIL, BOOTSTRAP_OWNER_PASSWORD, BOOTSTRAP_OWNER_NAME,
#             BOOTSTRAP_COMPANY_NAME, BOOTSTRAP_OPERATOR_EMAILS
#
# PRODUCTION: ASPNETCORE_ENVIRONMENT=Production makes every service refuse to start
# without a Key Vault (KEYVAULT_URI in secrets.env). The script stops before
# publishing if it is missing, and asks you to type 'production' to continue
# (set ASSUME_YES=1 to skip the question in an unattended run).
# ==============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
BRANCH="production"
ENV_NAME="Production"

# Deployment directories live outside the source tree
DEPLOY_BASE="/home/prabha/deploy/Production"
DIST_DIR="$DEPLOY_BASE/dist"
LOG_DIR="$DEPLOY_BASE/logs"
RUN_DIR="$DEPLOY_BASE/run"
SECRETS_FILE="${SECRETS_FILE:-$DEPLOY_BASE/secrets.env}"

# Addresses: callers use the public IP, services talk to each other on the LAN IP
PUBLIC_IP="117.247.103.65"
LAN_IP="192.168.1.101"
GATEWAY_PORT=7500
WEB_PORT=7200
API_BASE=7500          # service port = API_BASE + offset

DB_HOST="${DB_HOST:-$LAN_IP}"
DB_PORT="${DB_PORT:-7100}"
DB_USER="${DB_USER:-postgres}"
ADMIN_DB="${ADMIN_DB:-EP_Admin}"
TENANT_DB="${TENANT_DB:-IN000001}"

# name | project | offset | config key (BaseUrl / Seeding)
SERVICES=(
  "accounting|Accounting|01|Accounting"
  "customer|Customer|02|Customer"
  "inventory|Inventory|03|Inventory"
  "master|Master|04|Master"
  "purchase|Purchase|05|Purchase"
  "reporting|Reporting|06|Reporting"
  "sales|Sales|07|Sales"
  "printing|Printing|08|Printing"
  "employee|Employee|09|Employee"
  "timeleave|TimeLeave|10|TimeLeave"
  "payroll|Payroll|11|Payroll"
  "recruitment|Recruitment|12|Recruitment"
  "performance|Performance|13|Performance"
  "claims|Claims|14|Claims"
  "student|Student|15|Student"
  "admission|Admission|16|Admission"
  "attendance|Attendance|17|Attendance"
  "fee|Fee|18|Fee"
  "facility|Facility|19|Facility"
  "workorder|WorkOrder|20|WorkOrder"
  "preventive|Preventive|21|Preventive"
  "amc|MaintenanceContract|22|MaintenanceContract"
)

die() { echo "ERROR: $*" >&2; exit 1; }

load_secrets() {
  [ -f "$SECRETS_FILE" ] || die "$SECRETS_FILE not found. Create it (chmod 600) with DB_PASSWORD, INTERNAL_API_KEY and JWT_SIGNING_KEY."
  set -a
  # shellcheck disable=SC1090
  . "$SECRETS_FILE"
  set +a
  : "${DB_PASSWORD:?DB_PASSWORD missing in $SECRETS_FILE}"
  : "${INTERNAL_API_KEY:?INTERNAL_API_KEY missing in $SECRETS_FILE}"
  : "${JWT_SIGNING_KEY:?JWT_SIGNING_KEY missing in $SECRETS_FILE}"
  [ "${#JWT_SIGNING_KEY}" -ge 32 ] || die "JWT_SIGNING_KEY must be at least 32 characters."
  : "${KEYVAULT_URI:?KEYVAULT_URI missing in $SECRETS_FILE (Production refuses to start without a Key Vault)}"
}

export_environment() {
  export ASPNETCORE_ENVIRONMENT="$ENV_NAME"
  export DOTNET_ENVIRONMENT="$ENV_NAME"
  export ConnectionStrings__AdminDatabase="Host=$DB_HOST;Port=$DB_PORT;Database=$ADMIN_DB;Username=$DB_USER;Password=$DB_PASSWORD;Keepalive=30;Timeout=60;Command Timeout=120"
  export ConnectionStrings__TenantDatabase="Host=$DB_HOST;Port=$DB_PORT;Database=$TENANT_DB;Username=$DB_USER;Password=$DB_PASSWORD;Keepalive=30;Timeout=60;Command Timeout=120"
  export Internal__ApiKey="$INTERNAL_API_KEY"
  export Jwt__SigningKey="$JWT_SIGNING_KEY"
  export KeyVault__Uri="$KEYVAULT_URI"

  local item name project offset key port
  for item in "${SERVICES[@]}"; do
    IFS="|" read -r name project offset key <<< "$item"
    port=$((API_BASE + 10#$offset))
    export "${key}__BaseUrl=http://$LAN_IP:$port"
    export "Seeding__${key}=http://$LAN_IP:$port/"
  done

  [ -n "${BOOTSTRAP_OWNER_EMAIL:-}" ]     && export Bootstrap__OwnerEmail="$BOOTSTRAP_OWNER_EMAIL"
  [ -n "${BOOTSTRAP_OPERATOR_EMAILS:-}" ] && export Bootstrap__OperatorEmails="$BOOTSTRAP_OPERATOR_EMAILS"
  [ -n "${BOOTSTRAP_OWNER_PASSWORD:-}" ]  && export Bootstrap__OwnerPassword="$BOOTSTRAP_OWNER_PASSWORD"
  [ -n "${BOOTSTRAP_OWNER_NAME:-}" ]      && export Bootstrap__OwnerName="$BOOTSTRAP_OWNER_NAME"
  [ -n "${BOOTSTRAP_COMPANY_NAME:-}" ]    && export Bootstrap__CompanyName="$BOOTSTRAP_COMPANY_NAME"
  return 0
}

prepare_dirs() {
  mkdir -p "$DIST_DIR/backend" "$DIST_DIR/frontend/web" "$LOG_DIR" "$RUN_DIR"
}

stop_services() {
  echo ">>> Stopping $ENV_NAME services..."
  if [ -d "$RUN_DIR" ]; then
    for pid_file in "$RUN_DIR"/*.pid; do
      [ -f "$pid_file" ] || continue
      local pid
      pid="$(cat "$pid_file" 2>/dev/null || true)"
      if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
        kill "$pid" 2>/dev/null || true
        for _ in 1 2 3; do
          kill -0 "$pid" 2>/dev/null || break
          sleep 1
        done
        kill -9 "$pid" 2>/dev/null || true
      fi
      rm -f "$pid_file"
    done
  fi

  # Release this environment's ports only (web, gateway, APIs incl. 23 = Site)
  local port pids
  for port in "$WEB_PORT" "$GATEWAY_PORT" $(seq $((API_BASE + 1)) $((API_BASE + 23))); do
    pids=$(lsof -t -i :"$port" 2>/dev/null || true)
    if [ -n "$pids" ]; then
      # shellcheck disable=SC2086
      kill -9 $pids 2>/dev/null || true
    fi
  done
  echo ">>> All services stopped."
}

start_daemon() {
  local name="$1" bin_path="$2" port="$3"
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

  (
    cd "$(dirname "$bin_path")"
    if [ -n "$port" ]; then
      export ASPNETCORE_URLS="http://0.0.0.0:$port"
    fi
    nohup dotnet "$(basename "$bin_path")" </dev/null > "$log_file" 2>&1 &
    echo $! > "$pid_file"
  )
  echo "  [STARTED] $name (PID $(cat "$pid_file"), Port ${port:--})"
}

start_frontend() {
  local pid_file="$RUN_DIR/frontend.pid" log_file="$LOG_DIR/frontend.log"
  if [ -f "$pid_file" ] && kill -0 "$(cat "$pid_file")" 2>/dev/null; then
    echo "  [ALREADY RUNNING] Frontend SPA (PID $(cat "$pid_file"), Port $WEB_PORT)"
    return 0
  fi
  (
    WEB_ROOT="$DIST_DIR/frontend/web" PORT="$WEB_PORT" nohup node -e '
      const http = require("http"), fs = require("fs"), path = require("path");
      const PORT = Number(process.env.PORT), WEB_ROOT = process.env.WEB_ROOT;
      const MIME = { ".html": "text/html; charset=UTF-8", ".js": "application/javascript", ".css": "text/css", ".json": "application/json", ".png": "image/png", ".svg": "image/svg+xml", ".ico": "image/x-icon", ".woff2": "font/woff2" };
      http.createServer((req, res) => {
        const f = path.join(WEB_ROOT, path.normalize(decodeURIComponent(req.url.split("?")[0])).replace(/^(\.\.[\/\\])+/, ""));
        if (f.startsWith(WEB_ROOT) && fs.existsSync(f) && fs.statSync(f).isFile()) {
          res.writeHead(200, { "Content-Type": MIME[path.extname(f)] || "application/octet-stream" });
          fs.createReadStream(f).pipe(res);
        } else {
          const idx = path.join(WEB_ROOT, "index.html");
          if (fs.existsSync(idx)) { res.writeHead(200, { "Content-Type": "text/html; charset=UTF-8" }); fs.createReadStream(idx).pipe(res); }
          else { res.writeHead(404); res.end("index.html not found"); }
        }
      }).listen(PORT, "0.0.0.0");
    ' </dev/null > "$log_file" 2>&1 &
    echo $! > "$pid_file"
  )
  echo "  [STARTED] Frontend SPA (PID $(cat "$pid_file"), Port $WEB_PORT)"
}

start_services() {
  echo "=========================================================="
  echo " Starting $ENV_NAME services (database $DB_HOST:$DB_PORT)"
  echo "=========================================================="

  # Master first: it migrates the schema the others read
  echo "--- Master API ---"
  start_daemon "master" "$DIST_DIR/backend/master/Master.Api.dll" "$((API_BASE + 4))"
  sleep 4

  echo "--- Domain services ---"
  local item name project offset key
  for item in "${SERVICES[@]}"; do
    IFS="|" read -r name project offset key <<< "$item"
    [ "$name" = "master" ] && continue
    start_daemon "$name" "$DIST_DIR/backend/$name/$project.Api.dll" "$((API_BASE + 10#$offset))"
  done

  echo "--- Gateway ---"
  start_daemon "gateway" "$DIST_DIR/backend/gateway/Gateway.Api.dll" "$GATEWAY_PORT"

  echo "--- Background worker ---"
  start_daemon "worker" "$DIST_DIR/backend/worker/BillBook.Worker.dll" ""

  echo "--- Frontend ---"
  start_frontend

  sleep 3
  check_status
}

check_status() {
  prepare_dirs
  echo "=========================================================="
  echo " Bill-Book $ENV_NAME services status"
  echo "=========================================================="
  printf "%-22s %-8s %-12s %-10s\n" "SERVICE" "PORT" "STATUS" "PID"
  echo "----------------------------------------------------------"

  check_row() {
    local name="$1" port="$2" pid="" status="STOPPED" pid_file="$RUN_DIR/$1.pid"
    if [ -f "$pid_file" ]; then
      pid="$(cat "$pid_file" 2>/dev/null || true)"
      if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then status="RUNNING"; else status="DEAD"; pid=""; fi
    fi
    printf "%-22s %-8s %-12s %-10s\n" "$name" "${port:--}" "$status" "${pid:--}"
  }

  check_row "frontend" "$WEB_PORT"
  check_row "gateway" "$GATEWAY_PORT"
  local item name project offset key
  for item in "${SERVICES[@]}"; do
    IFS="|" read -r name project offset key <<< "$item"
    check_row "$name" "$((API_BASE + 10#$offset))"
  done
  check_row "worker" ""

  echo "----------------------------------------------------------"
  echo " Public endpoints:"
  echo "   - Web app:      http://$PUBLIC_IP:$WEB_PORT"
  echo "   - API gateway:  http://$PUBLIC_IP:$GATEWAY_PORT"
  echo "   - Database:     $DB_HOST:$DB_PORT ($ADMIN_DB)"
  echo "   - Build files:  $DIST_DIR"
  echo "=========================================================="
}

ensure_databases() {
  command -v psql >/dev/null 2>&1 || { echo "  [INFO] psql not installed; relying on the services to create nothing."; return 0; }
  export PGPASSWORD="$DB_PASSWORD"
  if ! psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -c "SELECT 1;" >/dev/null 2>&1; then
    die "Cannot reach PostgreSQL at $DB_HOST:$DB_PORT as $DB_USER."
  fi
  local db
  for db in "$ADMIN_DB" "$TENANT_DB"; do
    if [ "$(psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -t -A -c "SELECT 1 FROM pg_database WHERE datname = '$db';")" != "1" ]; then
      psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -c "CREATE DATABASE \"$db\" WITH OWNER $DB_USER ENCODING 'UTF8';"
    fi
  done
  unset PGPASSWORD
  echo "  [OK] Databases verified on $DB_HOST:$DB_PORT"
}

run_deployment() {
  echo "=========================================================="
  echo " Deploying $ENV_NAME from branch '$BRANCH'"
  echo " Time: $(date)"
  echo " Repo: $REPO_ROOT"
  echo " Target: $DEPLOY_BASE"
  echo "=========================================================="

  if [ "${ASSUME_YES:-0}" != "1" ]; then
    read -r -p "Deploy PRODUCTION from '$BRANCH' at $(git -C "$REPO_ROOT" rev-parse --short "origin/$BRANCH" 2>/dev/null || echo '?')? Type 'production' to continue: " answer
    [ "$answer" = "production" ] || die "Cancelled."
  fi

  cd "$REPO_ROOT"

  # 1. Take exactly what the branch holds. Fast-forward only: a deploy checkout
  #    never merges, commits or resets.
  echo -e "\n[1/5] Updating '$BRANCH' (fast-forward only)..."
  git fetch origin "$BRANCH"
  git checkout "$BRANCH"
  git merge --ff-only "origin/$BRANCH"
  [ -z "$(git status --porcelain --untracked-files=no)" ] || die "Working tree has local changes on '$BRANCH'."
  echo "  Deploying commit $(git rev-parse --short HEAD)"

  # 2. Databases
  echo -e "\n[2/5] Checking databases..."
  ensure_databases

  # 3. Backend
  echo -e "\n[3/5] Publishing backend services (.NET 10)..."
  local item name project offset key
  echo "  Publishing gateway..."
  dotnet publish "backend/Gateway/Gateway.Api/Gateway.Api.csproj" -c Release -p:EnvironmentName="$ENV_NAME" -o "$DIST_DIR/backend/gateway" --nologo -v q
  for item in "${SERVICES[@]}"; do
    IFS="|" read -r name project offset key <<< "$item"
    echo "  Publishing $name..."
    dotnet publish "backend/Api/$project/$project.Api/$project.Api.csproj" -c Release -p:EnvironmentName="$ENV_NAME" -o "$DIST_DIR/backend/$name" --nologo -v q
  done
  echo "  Publishing worker..."
  dotnet publish "backend/worker/BillBook.Worker/BillBook.Worker.csproj" -c Release -p:EnvironmentName="$ENV_NAME" -o "$DIST_DIR/backend/worker" --nologo -v q

  # 4. Frontend
  echo -e "\n[4/5] Building the web app with the '$BRANCH' configuration..."
  cd "$REPO_ROOT/frontend"
  [ -d node_modules ] || npm ci --legacy-peer-deps
  npx --yes nx build web -c production --base-href "/"
  rm -rf "$DIST_DIR/frontend/web"/*
  if [ -d "dist/apps/web/browser" ]; then
    cp -r dist/apps/web/browser/* "$DIST_DIR/frontend/web/"
  else
    cp -r dist/apps/web/* "$DIST_DIR/frontend/web/"
  fi
  rm -rf dist

  # 5. Restart
  echo -e "\n[5/5] Restarting $ENV_NAME services..."
  stop_services
  sleep 2
  start_services
}

case "${1:-deploy}" in
  start)   prepare_dirs; load_secrets; export_environment; start_services ;;
  stop)    prepare_dirs; stop_services ;;
  status)  check_status ;;
  deploy)  prepare_dirs; load_secrets; export_environment; run_deployment ;;
  *)       echo "Usage: $0 [deploy|start|stop|status]" >&2; exit 2 ;;
esac
