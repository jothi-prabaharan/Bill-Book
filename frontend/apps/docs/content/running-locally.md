# Running locally

## Prerequisites

- .NET SDK 10
- PostgreSQL 16+
- Node.js 20+
- `dotnet tool install --global dotnet-ef`

## One-press debugging in VS Code

Open the Run panel, choose **🚀 Everything (APIs + Gateway + Web)** and press **F5**. That builds the solution, launches all four backends with debuggers attached, starts the Angular dev server and opens one browser tab. Breakpoints work in C# and TypeScript at the same time. Stop once and the whole stack goes down.

There is also **Backend only** for when you do not want a browser, and each service individually.

## By hand

```bash
# Database — UTF-8 matters, multi-language support depends on it
createdb -E UTF8 retailerp_master

cd backend
dotnet build
dotnet ef migrations add Initial --project Api/Master/Master.Repository     --startup-project Api/Master/Master.Api
dotnet ef database update        --project Api/Master/Master.Repository     --startup-project Api/Master/Master.Api
# repeat for Identity and Platform
```

Then run the services — Identity on 5001, Platform on 5002, Master on 5003, Gateway on 5000.

```bash
cd frontend
npm install
npx nx serve web     # http://localhost:4200
npx nx serve docs    # http://localhost:4300 — this site
```

## Ports

| Port | Service |
|---|---|
| 5000 | Gateway (YARP) — has a status home page at `/` |
| 5001 | Identity |
| 5002 | Platform |
| 5003 | Master |
| 4200 | Web app |
| 4300 | Docs (this site) |

## First run caveats

The code has been written but **never compiled in the authoring environment** — there was no .NET SDK available. Expect the first `dotnet build` to surface fixes, most likely EF Core package versions and Angular/Nx version alignment. This is expected, not a sign something is wrong.

Two development stand-ins are in place and clearly marked in code: the email sender **logs the OTP to the console instead of sending mail** (convenient for testing the reset flow), and the secret store and event publisher are in-memory. Replace with SMTP, Key Vault and Service Bus for anything real.
