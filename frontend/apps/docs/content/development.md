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

## Typography — one monospace stack, every app

Every font role resolves to a single monospace stack, defined once in
`libs/shared/theming/src/lib/_tokens.scss`:

```scss
--font-stack-mono: ui-monospace, SFMono-Regular, "SF Mono", Menlo, Consolas,
  "Liberation Mono", "Courier New", monospace;
--font-heading: var(--font-stack-mono);
--font-body:    var(--font-stack-mono);
--font-mono:    var(--font-stack-mono);
```

**Why monospace.** A monospace font gives every glyph one advance width, so a
column of figures lines up without any per-element opt-in. The previous pairing
(Cormorant Garamond headings, Lora body) was proportional: `1` was 41% narrower
than `0` in Lora, so `1,111.11` and `9,999.99` in the same column ended at
different places unless that element had explicitly asked for the `tnum`
OpenType feature. Every amount cell that forgot the class drifted, and one
shared grid had never asked at all.

The `font-variant-numeric: tabular-nums` rules throughout the theme are now
redundant — they are no-ops against a monospace face — and are kept so the
opt-in still works if a proportional font is ever reinstated.

**System fonts only.** Nothing is fetched from Google Fonts, so there is no
webfont request, no flash of unstyled text, and no third-party dependency in
the render path. The stack covers macOS (`ui-monospace`, SF Mono, Menlo),
Windows (Consolas) and Linux (Liberation Mono), landing on the generic
`monospace` if none match.

**Where the tokens reach.** `apps/web` imports the full theming entry point and
`apps/desktop` inherits it by importing web's stylesheet. `apps/admin` and
`apps/portal` import the same entry point. `apps/docs` imports only the tokens
partial — it keeps its own component styling and takes just the font roles.
A new app gets the fonts by importing `libs/shared/theming/src/index.scss`.

## By hand

```bash
# Database — UTF-8 matters, multi-language support depends on it
createdb -E UTF8 EP_Admin
createdb -E UTF8 EP_Tenant

cd backend
dotnet build
# Master maps two contexts, so it needs --context on each.
dotnet ef database update --project Api/Master/Master.Repository \
  --startup-project Api/Master/Master.Api --context AdminDbContext
dotnet ef database update --project Api/Master/Master.Repository \
  --startup-project Api/Master/Master.Api --context ContactsDbContext
dotnet ef database update --project Api/Inventory/Inventory.Repository \
  --startup-project Api/Inventory/Inventory.Api
dotnet ef database update --project Api/Accounting/Accounting.Repository \
  --startup-project Api/Accounting/Accounting.Api
```

Then run the services — see the port table below, Gateway on 5000 — with `ASPNETCORE_ENVIRONMENT=Development` set. That is what picks up `appsettings.Development.json`, the only one of the four environment files carrying real local values; the others are blank on purpose. The local connection string lives there, not in `appsettings.json`, so there is nothing to edit before the first run. See [Environments](#/environments).

```bash
cd frontend
npm install
npx nx serve web     # http://localhost:4200
npx nx serve docs    # http://localhost:4300 — this site
```

Both default to the `development` configuration. Add `--configuration staging` to serve a production-shaped build that still has source maps.

## Checks

```bash
cd frontend
npm run lint     # ESLint across every project
npm run test     # Vitest, once
npm run check    # lint, then test, then build — what to run before pushing
```

`npm run test:watch` re-runs on save.

**Lint** is one flat config at `frontend/eslint.config.mjs`, and Nx infers a
`lint` target for every project from it — a new library is linted the day it is
created rather than when someone remembers to wire it up. The rules that are on
beyond the recommended sets are there to catch defects, not to enforce a style:
floating promises (a failed save that reports success), misused promises (an
`async ngOnInit`, which Angular never awaits), unused variables, and the
component-selector prefix.

**Tests** run on Vitest with jsdom, from one config at
`frontend/vitest.config.mts`. Services, guards and interceptors are testable
today; component tests are not, because they need templates compiled and that
means adding the Angular Vite plugin. The Angular `unit-test` builder was tried
and rejected — Angular marks it experimental, and as configured it insists on a
real browser to run tests that do not need one.

Backend tests live in `backend/tests/` and run with `dotnet test` — 58 of them,
over pure logic only: number composition, drag-ordering and the phone attributes
in `Shared.Kernel`, and what a stock movement means in the general ledger.

## Calling the API by hand

`postman/Bill-Book.postman_collection.json` — 204 requests across every service,
**generated from the controllers** by `python3 postman/generate.py`. Import it,
set `email` and `password`, then run **① Login** and **② Select organization**:
they capture the tokens into collection variables and everything else inherits
the bearer token.

It is generated rather than maintained because a hand-kept collection drifts
from the API within a fortnight — a route is renamed, a field is added, and the
collection goes on sending a body the server stopped accepting. Three things it
works out rather than being told: which host a request belongs to (the gateway
only proxies some prefixes), which guard applies (anonymous, bearer, or the
shared internal key, read from the attributes), and what a body looks like
(built from the C# request model, so a new field appears the day it is added).

Re-run the script after changing a controller, and commit the result with it.

## Ports

| Port | Service |
|---|---|
| 4500 | Gateway (YARP) — has a status home page at `/` |
| 4503 | Master — reference data, tenancy, auth, contacts |
| 4504 | Accounting — the ledger and the money documents |
| 4506 | Inventory |
| 5008 | Customer *(scaffold)* |
| 5009 | Purchase *(scaffold)* |
| 5010 | Reporting *(scaffold)* |
| 4507 | Sales *(schema only)* |
| 4200 | Web app |
| 4300 | Docs (this site) |

Each surviving service kept the port it already had, so 5001, 5002, 5005, 5007
and 5012 are now free — they belonged to Identity, Platform, Contacts, Banking
and Support.

## First run caveats

The backend builds, tests and migrates cleanly as of 2 August 2026: `dotnet build` with zero warnings, 58 tests passing, every EF snapshot matching its model, and all 29 migrations applied to PostgreSQL 16.

If a container has no `dotnet`, install it from the distribution repository rather than the install script — some environments deny `dot.net` by egress policy:

```bash
apt-get update && apt-get install -y dotnet-sdk-10.0
```

Two development stand-ins are in place and clearly marked in code: the email sender **logs the OTP to the console instead of sending mail** (convenient for testing the reset flow), and the secret store and event publisher are in-memory. Replace with SMTP, Key Vault and Service Bus for anything real.




# Transactions and errors

Both are wired once per service and neither is a controller's concern.

## Every write runs in a transaction

`AddBillBookReliability<TContext>()` in a service's `Program.cs` adds
`TransactionFilter` to the MVC pipeline. It opens a transaction before any
`POST`, `PUT`, `PATCH` or `DELETE` action and settles it afterwards. Reads take
none.

**Commit is earned by the status the action returned, not by the absence of an
exception.** Anything from 400 up rolls back, as does `Forbid()`, a cancelled
action and any exception. A service that wrote two of three rows and returned
`Conflict()` keeps neither of them.

Inside a service, never call `BeginTransactionAsync` — it throws the moment
something above it has already started one. Use:

```csharp
await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);
// ... work ...
await tx.CommitAsync(ct);
```

which opens a transaction or joins the one already open, making its own commit
and rollback no-ops so the outermost owner decides. Disposing without committing
rolls back.

To need more than Read Committed, put it on the action —
`[Transactional(IsolationLevel.Serializable)]` — not inside the method. Postgres
only accepts `SET TRANSACTION ISOLATION LEVEL` before a transaction's first
statement, so asking from an inner service is always too late; `BeginScopeAsync`
throws and names the attribute to add rather than quietly giving you the weaker
level. `[NoTransaction]` opts an action out.

**What this does not cover.** The filter protects this service's own rows. A
posting path that calls Inventory and then Accounting over HTTP has already
committed in those services, and no rollback here undoes it. Those steps are safe
because they are idempotent — the ledger replaces rows for a given
`(TransactionTypeCode, TransactionId)`, Inventory dedupes on
`(SourceType, SourceId, SourceLineId)` — so a retry corrects a half-finished
post. That is a saga, not a transaction, and it is worth knowing which one you
are relying on.

## Every failure is translated, recorded, and answered

`GlobalExceptionHandler` runs first in the pipeline, before authentication.

**Translate.** `SqlErrorCatalog` maps SQLSTATE to a status, an `ApiErrorCode` and
a sentence — exact code first, then its two-character class, then `Unexpected`.
Nothing branches on an exception's message. A new state goes in the catalogue.

**Answer.** The body is the same shape everywhere:

```json
{
  "code": "DuplicateRecord",
  "message": "A record with these details already exists.",
  "status": 409,
  "errorReference": "0f2e...",
  "traceId": "00-...",
  "isTransient": false,
  "diagnostics": null
}
```

`code` and `message` are identical in every environment — branch on `code`, show
`message`. `diagnostics` is the difference:

| | Development | Everywhere else |
|---|---|---|
| `diagnostics` | the exact error — Postgres's own message, SQLSTATE, constraint, table, schema, `DETAIL`, `HINT`, the plpgsql `WHERE`, the EF entries and the stack | `null` |
| `message` | the curated sentence | the same curated sentence |

The switch is `IHostEnvironment.IsDevelopment()`. **There is deliberately no
configuration key that turns detail on in Production** — a setting like that is
one deployment mistake from publishing the schema, and the error reference gets
support to the same information without publishing anything.

**Record.** The unabridged failure goes to the service's own `ErrorLogs` table —
`acc.ErrorLogs`, `sal.ErrorLogs`, one per schema, each owned and migrated by its
own service. The caller is handed `errorReference` and nothing else; no endpoint
returns rows from these tables.

`ErrorLogs` is tenant scoped like every other table: `CustomerId`, `OrgId`, the
query filter, and an RLS policy that is ENABLEd and FORCEd. **The consequence is
accepted rather than overlooked — an error raised before the tenant is known
cannot be recorded at all.** A failed sign-in, a request with no token, a startup
fault: those go to `ILogger` and the response carries a trace id with no
reference on it.

## Workers audit instead

A worker gets no filter and no handler — it serves no requests, and where it
needs atomicity it opens its own scope. What it gets is
`IWorkerErrorAuditor.AuditAsync(workerName, jobReference, exception, ct)`, which
writes the failure with `Source = Worker` and `FollowUpStatus = Open`.

That matters because **nobody is watching**. An API failure produces a response
somebody reads and usually retries; a worker failure produces a log line on a
server, which is the same as producing nothing. The set of rows still `Open` is
the task list, and `JobReference` names what the run was working on — a
`StockMovementId`, an organization — so the follow-up has something to act on.

# Environments

**Status: built.**

Four environments — **Development**, **Staging**, **UAT**, **Production**. Both halves of the stack know all four, but they select one in different ways and at different times: the backend at **run time** from a variable, the frontend at **build time** from a file swap.

## The two selection mechanisms

| | Backend | Frontend |
|---|---|---|
| Chosen by | `ASPNETCORE_ENVIRONMENT` | Nx configuration on the build |
| Chosen at | Process start | Build |
| Mechanism | `appsettings.{Environment}.json` layered over `appsettings.json` | `fileReplacements` swapping `environment.ts` |
| Changing it | Restart with a different variable | Rebuild |

A deployed Angular bundle **is** its environment — there is no runtime switch, because the values are compiled in. Nothing to configure on the box, and nothing to forget to configure either.

## Backend

The four runnable projects with real configuration — Master, Accounting, Inventory and the Gateway — each carry five files:

```
Master.Api/
  appsettings.json                shared, non-secret defaults + blank placeholders
  appsettings.Development.json    real local values
  appsettings.Staging.json        blank placeholders
  appsettings.UAT.json            blank placeholders
  appsettings.Production.json     blank placeholders
```

`appsettings.json` is loaded first, then the file matching `ASPNETCORE_ENVIRONMENT` on top of it. Only the keys that differ appear in the environment file — log levels, connection strings, service base URLs — so the shape of the configuration lives in one place.

### Development carries real values

Development is the only file with anything in it, because the values are the same on every machine and none of them are secret:

```
Host=localhost;Port=5432;Database=EP_Admin;Username=postgres;Password=123
```

Clone, `createdb`, press **F5**. Nothing to set up by hand. The launch configurations in `.vscode/launch.json` all set `ASPNETCORE_ENVIRONMENT=Development`, so this is what you get unless you go out of your way.

### Everything else ships blank

Staging, UAT and Production carry the same keys with **empty strings**. They are supplied at run time from the environment.

> **Blank rather than absent is deliberate.** A key that is missing falls back to whatever the layer below it says — which for a hand-rolled default means a localhost connection string compiled into the binary, quietly connecting Production to nothing. An empty value fails at startup instead. Loud beats silent.

### Supplying values

ASP.NET Core maps environment variables onto configuration keys by replacing `:` with a **double underscore**, which is what container runtimes and app services accept.

| Configuration key | Environment variable | Set for |
|---|---|---|
| `ConnectionStrings:AdminDatabase` | `ConnectionStrings__AdminDatabase` | Master |
| `ConnectionStrings:DesignTimeDatabase` | `ConnectionStrings__DesignTimeDatabase` | Master, Accounting, Inventory |
| `ConnectionStrings:TenantFallback` | `ConnectionStrings__TenantFallback` | Master, Accounting, Inventory |
| `TenantDatabase:ConnectionTemplate` | `TenantDatabase__ConnectionTemplate` | Master |
| `Jwt:SigningKey` | `Jwt__SigningKey` | every service |
| `Encryption:Key` | `Encryption__Key` | Master |
| `Master:BaseUrl` | `Master__BaseUrl` | Accounting, Inventory, Sales, CostingEngine |
| `Accounting:BaseUrl` | `Accounting__BaseUrl` | Master, Sales, CostingEngine |
| `Inventory:BaseUrl` | `Inventory__BaseUrl` | Accounting |
| `App:BaseUrl` | `App__BaseUrl` | Master |

Master takes both connection strings, and that is the merge showing through the
configuration: `AdminDatabase` is the shared master database its `mst` schema
lives in, and `DesignTimeDatabase` is the fallback its contacts context uses when
no tenant has been resolved.

The Gateway's destinations follow the same rule, one level deeper — a cluster id and a destination id sit in the path:

```
ReverseProxy__Clusters__master__Destinations__d1__Address
ReverseProxy__Clusters__accounting__Destinations__d1__Address
ReverseProxy__Clusters__inventory__Destinations__d1__Address
ReverseProxy__Clusters__sales__Destinations__d1__Address
```

The **routes** are not environment-dependent and stay in `appsettings.json`. Only where each cluster points changes between environments.

`Jwt:Issuer`, `Jwt:Audience` and the token lifetimes are non-secret product decisions, so they live in `appsettings.json` and are the same everywhere. `Jwt__SigningKey` must be **identical across every service** in a given environment — Master issues the token and everything else validates it.

## Frontend

`apps/web` and `apps/docs` each have:

```
src/environments/
  environment.model.ts        the interface every file satisfies
  environment.ts              Development — the file the app imports
  environment.staging.ts
  environment.uat.ts
  environment.production.ts
```

Application code always imports `./environments/environment`. The build replaces the file underneath it, so no code anywhere branches on which environment it is in.

Each configuration in `project.json` names its replacement:

| Configuration | Replaces `environment.ts` with | Optimised | Source maps |
|---|---|---|---|
| `development` | *(nothing — used as-is)* | no | yes |
| `staging` | `environment.staging.ts` | yes | yes |
| `uat` | `environment.uat.ts` | yes | no |
| `production` | `environment.production.ts` | yes | no |

Staging is a production-shaped build that keeps source maps, which is why it is the only non-development configuration you can usefully debug.

```bash
cd frontend

npx nx serve web --configuration staging     # 4200
npx nx serve docs --configuration staging    # 4300

npx nx build web  --configuration uat
npx nx build web  --configuration production
```

Omitting `--configuration` serves `development` and builds `production` — the defaults each target declares.

### `apiBaseUrl` and the interceptor

The web environment exposes one value beyond the flags: `apiBaseUrl`, the origin the API is served from, without a trailing slash.

Call sites do **not** use it. They keep issuing root-relative `'/api/...'` requests, and `apiBaseUrlInterceptor` in `libs/shared/api-client` prefixes the configured origin when there is one. `apps/web/src/app/app.config.ts` supplies it:

```ts
{ provide: API_BASE_URL, useValue: environment.apiBaseUrl }
```

The `API_BASE_URL` injection token exists so the library never imports app-level config — that would invert the `app → lib` dependency direction. The interceptor runs **before** the auth interceptor: rewrite the URL, then attach the bearer token.

**Empty means same origin.** Development leaves it empty because `proxy.conf.json` already forwards `/api/*` to the individual services on 5001–5004. Deployed environments have no such proxy, so either point `apiBaseUrl` at the Gateway origin or serve the app from behind the Gateway and leave it empty.

The docs app has no `apiBaseUrl` — it reads no API. It carries the production flag and the environment name, and nothing else.

## Debugging

Only **Development** and **Staging** are debug targets. UAT and Production are deploy-time build configurations; there is nothing to attach to locally, and pretending otherwise just invites someone to debug against a bundle that is not what shipped.

| Launch configuration | Serves | Port |
|---|---|---|
| Web · Development (Chrome, 4200) | `web` at `development` | 4200 |
| Web · Staging (Chrome, 4200) | `web` at `staging` | 4200 |
| Docs · Development (Chrome, 4300) | `docs` at `development` | 4300 |
| Docs · Staging (Chrome, 4300) | `docs` at `staging` | 4300 |
| Web (Chrome, attach 9222) | attaches to a running tab | 4200 |
| Docs (Chrome, attach 9222) | attaches to a running tab | 4300 |

Two compounds run both apps at once: **Frontend only (Web + Docs, Development)** and **Frontend only (Web + Docs, Staging)**.

Each launch configuration has a matching task — `serve-web`, `serve-web-staging`, `serve-docs`, `serve-docs-staging` — and all four run `.vscode/serve-angular.js` with a project, a port and a configuration. See [Running locally](#/running-locally) for why that wrapper exists and what breaks if it is removed.

## Apps without environment files

`apps/portal` and `apps/desktop` are empty shells with no build targets. They get environment files and debug configurations when they get something to build. `apps/admin` has both now — it builds, lints and serves like `web` and `docs` do.

The backend services that are not yet implemented — Customer, Purchase, Reporting — have only a placeholder `appsettings.json`. Per-environment files arrive with the service.

## Service-to-service key

Services call each other for things no user token covers: signup and platform
admin both write each service's master data for a new organization through the
same seeding call.

Three of the calls that used to be on this list are gone. Resolving an
organization's account on sign-in was Identity asking Platform, reading the
tenant directory for contacts was Contacts asking Platform, and resolving a
customer's own database connection was every other service asking Master on
every request; all three are gone now — the first two because Identity, Platform
and Contacts are one service, the third because there is no longer a
connection to resolve; every service opens the one shared tenant database at
startup. What is left calls Master for the same reason those did: no user token
covers it, so the endpoint takes the organization or customer to act on as a
parameter — which is exactly what makes it dangerous, and why it is guarded by
a shared key instead.

Every route beginning `internal/` carries that guard. They are also absent from
the gateway and so unreachable from outside the cluster, but that is a routing
detail rather than a control, which is why the key exists as well.

| Setting | Where |
|---|---|
| `Internal:ApiKey` | Every service. All of them both send and receive. |

They must all hold the **same** value. Development ships a throwaway key so a
clone runs with no setup; Staging, UAT and Production ship blank and read it from
`Internal__ApiKey` in the environment.

A blank key on a receiver **refuses every internal call** rather than accepting
them, so a misconfigured deployment fails loudly instead of leaving an open
endpoint that will seed, reseed or read any organization it is given the id of.

## What is reachable without signing in

Three things, and only three:

- **Sign-in itself** — login, organization selection, password reset by code, and
  accepting an invitation. All of these necessarily happen before a token exists.
- **Signup**, including the status poll the progress screen makes while the
  account is being provisioned.
- **Countries and their states**, because the signup form asks for them before
  there is an account to authenticate.

Everything else requires a token. That is enforced by a default-deny policy in
each service rather than by an attribute on each controller, so a controller
added tomorrow is authenticated because nobody did anything — the three
exceptions above say so explicitly, and are visible as such.

The same rule covers reference data that is not secret but is nobody else's
business either. Currencies, HSN/SAC codes and the account and ledger type
masters are read only from inside the app, so they are not served to whoever
finds the port; the two lookups other services need have their own key-guarded
route rather than being opened up.



# Conventions

The binding rules live in `CLAUDE.md` at the repository root. This page summarises the ones you will hit first.

## Data access

- **LINQ only.** The only raw SQL permitted is `CREATE DATABASE`, RLS policies, triggers and `set_config` — everything else has a LINQ equivalent and must use it.
- **PostgreSQL only.** RLS, `xmin` and JSONB are used deliberately; do not add SQL Server compatibility or avoid a Postgres feature for portability.
- **PascalCase tables and columns**, matching the C# property names exactly. Postgres needs quoted identifiers for that, which is expected.

## Entities

- Plain property bags — no constructors, no methods, no computed properties, no validation logic
- **Every Data Annotation carries an `ErrorMessage`**
- All inherit `AuditableEntity`
- **Enums, never magic strings**, for any fixed set of values

## Audit columns

Every table has `CreatedBy`, `CreatedAt`, `ModifiedBy`, `ModifiedAt`, and **all four are nullable**. `CreatedBy IS NULL` marks system/seed data — a row written by no user. They are set only by `AuditSaveChangesInterceptor`; never assign them by hand.

## Tenancy

Every table in a per-customer schema carries `CustomerId` and `OrgId` plus a global query filter and an RLS policy. **A missing filter leaks data between organizations, or between customers** — this is the highest-consequence mistake in the codebase.

Every unique constraint that used to include only `OrgId` keeps only `OrgId` — it was already globally unique, so `CustomerId` is defence in depth on the filter, not a new dimension existing keys need to widen into.

## Service boundaries

Never reference another service's `DbContext`. Read over its API through a named seam; write by publishing an event.

## Frontend components

Three files per page or component, sharing a base name: `.ts`, `.html`, `.scss`. The decorator uses `templateUrl` and `styleUrl` — **no inline `template:` or `styles:` blocks**. A component with no styles of its own still gets a `.scss` file, so the trio is the same everywhere.

`-core` libs stay Ionic-compatible: Signals and DI are fine, but no `window`, no `document`, no Syncfusion, no Electron or Node APIs. Every page works at ~360px.

## Documentation

**Update this site in the same commit as the feature.** See [Release notes](#/release-notes) for how that flows into a version entry.



