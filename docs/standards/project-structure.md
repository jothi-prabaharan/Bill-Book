# Project Structure

This is a multi-tenant retail ERP and accounting system.

## Root Directories
- `backend/`: The .NET 10 solution containing the API and various bounded context modules.
- `frontend/`: The client application (React/Next.js/Nx monorepo style).
- `docs/`: Unified documentation containing architecture flows, module schemas, and project standards.
- `scripts/`: Powershell and SQL scripts for local developer setup (e.g., setting up the dev database and seeding it).

## Backend Modules (`backend/Api/`)
- `Accounting`
- `Customer`
- `Inventory`
- `Master`
- `Purchase`
- `Reporting`
- `Sales`

Each module generally maintains its own domain entities, EF Core DbContext, migrations, and API endpoints.
