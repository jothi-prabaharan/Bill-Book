#!/usr/bin/env bash

DEPLOY_BASE="${DEPLOY_DIR:-/home/prabha/deploy/Stating}"
RUN_DIR="$DEPLOY_BASE/run"

echo "=========================================================="
echo " Bill-Book Staging Services Status"
echo "=========================================================="
printf "%-22s %-8s %-12s %-10s\n" "SERVICE" "PORT" "STATUS" "PID"
echo "----------------------------------------------------------"

check_service() {
  local name="$1"
  local port="$2"
  local pid_file="$RUN_DIR/$name.pid"
  local pid=""
  local status="STOPPED"

  if [ -f "$pid_file" ]; then
    pid="$(cat "$pid_file" 2>/dev/null || true)"
    if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
      status="RUNNING"
    else
      status="DEAD (stale pid)"
      pid=""
    fi
  fi

  if [ -n "$port" ]; then
    if nc -z localhost "$port" 2>/dev/null || ss -tulpn | grep -q ":$port "; then
      if [ "$status" != "RUNNING" ]; then
        status="LISTENING (ext)"
      fi
    elif [ "$status" == "RUNNING" ]; then
      status="STARTING..."
    fi
  fi

  printf "%-22s %-8s %-12s %-10s\n" "$name" "${port:--}" "$status" "${pid:--}"
}

check_service "frontend" "5200"
check_service "gateway" "5500"
check_service "accounting" "5501"
check_service "customer" "5502"
check_service "inventory" "5503"
check_service "master" "5504"
check_service "purchase" "5505"
check_service "reporting" "5506"
check_service "sales" "5507"
check_service "printing" "5508"
check_service "employee" "5509"
check_service "timeleave" "5510"
check_service "payroll" "5511"
check_service "recruitment" "5512"
check_service "performance" "5513"
check_service "claims" "5514"
check_service "student" "5515"
check_service "admission" "5516"
check_service "attendance" "5517"
check_service "fee" "5518"
check_service "facility" "5519"
check_service "workorder" "5520"
check_service "preventive" "5521"
check_service "amc" "5522"
check_service "worker" ""

echo "----------------------------------------------------------"
echo " Public Access Endpoints:"
echo "   - Web App UI:       http://localhost:5200"
echo "   - API Gateway:      http://localhost:5500"
echo "   - Swagger Docs:     http://localhost:5500/swagger"
echo "   - Staging DB:       192.168.1.101:5100 (Database: EP_Admin)"
echo "=========================================================="
