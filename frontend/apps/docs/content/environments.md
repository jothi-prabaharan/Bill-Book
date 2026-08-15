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

`apps/portal`, `apps/admin` and `apps/desktop` are empty shells with no build targets. They get environment files and debug configurations when they get something to build.

The backend services that are not yet implemented — Customer, Purchase, Reporting — have only a placeholder `appsettings.json`. Per-environment files arrive with the service.

## Service-to-service key

Services call each other for things no user token covers: Master's provisioning
worker writes each service's master data for a new organization, and Accounting,
Inventory and Sales ask Master which database to open on every request.

Two of the calls that used to be on this list are gone. Resolving an
organization's account on sign-in was Identity asking Platform, and reading the
tenant directory for contacts was Contacts asking Platform; all three are Master
now, so both are queries. None of those callers has a user token, so those endpoints take the
organization or customer to act on as a parameter — which is exactly what makes
them dangerous, and why they are guarded by a shared key instead.

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
