#!/usr/bin/env bash

DEPLOY_BASE="${DEPLOY_DIR:-/home/prabha/deploy/Stating}"
RUN_DIR="$DEPLOY_BASE/run"

echo "=========================================================="
echo " Stopping Bill-Book Staging Services..."
echo "=========================================================="

if [ -d "$RUN_DIR" ]; then
  for pid_file in "$RUN_DIR"/*.pid; do
    if [ -f "$pid_file" ]; then
      name="$(basename "$pid_file" .pid)"
      pid="$(cat "$pid_file" 2>/dev/null || true)"
      if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
        echo "  Stopping $name (PID $pid)..."
        kill "$pid" 2>/dev/null || true
        for i in {1..5}; do
          if ! kill -0 "$pid" 2>/dev/null; then
            break
          fi
          sleep 1
        done
        if kill -0 "$pid" 2>/dev/null; then
          echo "    Force killing $name (PID $pid)..."
          kill -9 "$pid" 2>/dev/null || true
        fi
      fi
      rm -f "$pid_file"
    fi
  done
fi

# Ensure staging ports are cleared: 5200, 5500-5522
echo "  Ensuring staging ports 5200, 5500-5522 are released..."
for port in 5200 $(seq 5500 5522); do
  pids=$(lsof -t -i :"$port" 2>/dev/null || true)
  if [ -n "$pids" ]; then
    echo "    Clearing lingering process on port $port (PIDs: $pids)..."
    kill -9 $pids 2>/dev/null || true
  fi
done

echo "=========================================================="
echo " All Staging Services Stopped."
echo "=========================================================="
