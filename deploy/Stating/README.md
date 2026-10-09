# Bill-Book Staging Deployment

This folder contains the complete **One-Click Deployment system** for the **Staging environment** on Linux.

---

## Architecture & Ports

| Component | Port | Host/URL |
|---|---|---|
| **Staging PostgreSQL Cluster (18/stg)** | `5100` | `192.168.1.101:5100` (Database: `EP_Admin`, `IN000001`) |
| **Frontend Web SPA** | `5200` | `http://localhost:5200` |
| **Backend API Gateway (YARP)** | `5500` | `http://localhost:5500` |
| **Swagger UI Portal** | `5500` | `http://localhost:5500/swagger` |
| **Microservice APIs (Internal)** | `5501–5522` | `http://localhost:5501` to `http://localhost:5522` |
| **BillBook.Worker** | Background | Internal daemon for scheduled reminders, costing, rate sync |

---

## How to Deploy

### Option 1: One-Click GUI Desktop Shortcut
Double-click the **Deploy Staging (Bill-Book)** launcher icon on your Ubuntu Desktop.

### Option 2: Command Line (One Command)
Run:
```bash
cd deploy/Stating
./deploy.sh
```

To specify a custom branch to deploy from (defaults to `Staging`, or `staging`, or `main`):
```bash
./deploy.sh Staging
```

---

## Management Commands

* **Start services**:
  ```bash
  ./start.sh
  ```
* **Stop all services**:
  ```bash
  ./stop.sh
  ```
* **Check status**:
  ```bash
  ./status.sh
  ```
* **View Logs**:
  All service logs are recorded under `deploy/Stating/logs/`:
  ```bash
  tail -f logs/gateway.log
  tail -f logs/master.log
  tail -f logs/frontend.log
  ```

---

## Directory Structure

```
deploy/Stating/
├── deploy.sh               # Master one-click deployment script
├── start.sh                # Starts all staging backend & frontend services
├── stop.sh                 # Gracefully terminates all staging services
├── status.sh               # Displays live status of services and ports
├── serve-frontend.js       # Lightweight Node.js server for the Angular SPA on port 5200
├── one-click-deploy.desktop # Desktop launcher shortcut
├── dist/                   # Published binaries and compiled Angular bundle
│   ├── backend/            # Gateway, 22 microservices, BillBook.Worker
│   └── frontend/web/       # Angular web app bundle
├── logs/                   # Service log files
└── run/                    # Process PID tracking files
```
