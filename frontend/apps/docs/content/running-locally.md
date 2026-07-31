# Running locally

## Prerequisites

- .NET SDK 10
- PostgreSQL 16+
- Node.js 20+
- `dotnet tool install --global dotnet-ef`

## One-press debugging in VS Code

Open the Run panel, choose **🚀 Everything (APIs + Gateway + Web)** and press **F5**. That builds the solution, launches the four APIs and the Gateway with debuggers attached, starts the Angular dev server and opens one browser tab. Breakpoints work in C# and TypeScript at the same time. Stop once and the whole stack goes down.

There is also **Backend only (APIs + Gateway)** for when you do not want a browser, each service individually, and two frontend compounds — **Frontend only (Web + Docs, Development)** and **Frontend only (Web + Docs, Staging)**.

Every .NET launch configuration sets `ASPNETCORE_ENVIRONMENT=Development`, and the frontend configurations are named for the Nx configuration they serve. Only Development and Staging are debuggable; see [Environments](#/environments) for why, and for what the other two are.

If Chrome is already open on a debugging port, **Web (Chrome, attach 9222)** and **Docs (Chrome, attach 9222)** attach to the running tab instead of launching a second browser. **Attach to .NET process** picks up a service you started from a terminal.

### How the debug wiring fits together

A few pieces are load-bearing and easy to break by "tidying" them:

- **`build-backend` builds `Bill-Book.Debug.slnf`, not the full solution.** The filter contains only the five runnable projects. Building all 36 on every F5 is slow, and rebuilding a project whose process is already running fails on a locked DLL.
- **The serve tasks run `.vscode/serve-angular.js`, not `npx nx serve` directly.** When the port is taken, `@angular/build` asks "use a different port?" and waits — the guard meant to suppress that prompt (`if (!tty_1.isTTY)` in `check-port.js`) tests a function object rather than calling it, so it is always false and the prompt always appears. In a debug session that hangs the preLaunchTask forever and the browser never opens. The wrapper reuses an existing dev server instead. It takes a project, a port and a configuration, which is how one script drives all four tasks — `serve-web`, `serve-web-staging`, `serve-docs`, `serve-docs-staging`.
- **Those tasks must stay `isBackground: true` with a `background` matcher.** The `endsPattern` is what tells VS Code the dev server is ready. Without it, Chrome launches before the server is listening and lands on a connection error.
- **`resolveSourceMapLocations` excludes `node_modules`.** Without it the debugger tries to resolve vendor source maps and breakpoints in your own components bind unreliably.

## Frontend page structure

Every page and component is three files with a shared base name — `.ts`, `.html`, `.scss`:

```
libs/accounting/accounting-ui/src/lib/
  tax-master.page.ts       @Component with templateUrl + styleUrl
  tax-master.page.html     template
  tax-master.page.scss     component-scoped styles
```

No inline `template:` or `styles:` blocks. Components with no styling of their own still get a `.scss` file so the trio is predictable — editors, search and the "go to file" list all behave the same for every page. Styles are SCSS, matching the app-level `styles.scss`.

## By hand

```bash
# Database — UTF-8 matters, multi-language support depends on it
createdb -E UTF8 retailerp_master

cd backend
dotnet build
dotnet ef migrations add Initial --project Api/Master/Master.Repository     --startup-project Api/Master/Master.Api
dotnet ef database update        --project Api/Master/Master.Repository     --startup-project Api/Master/Master.Api
# repeat for Identity, Platform and Accounting
```

Then run the services — Identity on 5001, Platform on 5002, Master on 5003, Accounting on 5004, Gateway on 5000 — with `ASPNETCORE_ENVIRONMENT=Development` set. That is what picks up `appsettings.Development.json`, the only one of the four environment files carrying real local values; the others are blank on purpose. The local connection string lives there, not in `appsettings.json`, so there is nothing to edit before the first run. See [Environments](#/environments).

```bash
cd frontend
npm install
npx nx serve web     # http://localhost:4200
npx nx serve docs    # http://localhost:4300 — this site
```

Both default to the `development` configuration. Add `--configuration staging` to serve a production-shaped build that still has source maps.

## Ports

| Port | Service |
|---|---|
| 5000 | Gateway (YARP) — has a status home page at `/` |
| 5001 | Identity |
| 5002 | Platform |
| 5003 | Master |
| 5004 | Accounting |
| 4200 | Web app |
| 4300 | Docs (this site) |

## First run caveats

The code has been written but **never compiled in the authoring environment** — there was no .NET SDK available. Expect the first `dotnet build` to surface fixes, most likely EF Core package versions and Angular/Nx version alignment. This is expected, not a sign something is wrong.

Two development stand-ins are in place and clearly marked in code: the email sender **logs the OTP to the console instead of sending mail** (convenient for testing the reset flow), and the secret store and event publisher are in-memory. Replace with SMTP, Key Vault and Service Bus for anything real.
