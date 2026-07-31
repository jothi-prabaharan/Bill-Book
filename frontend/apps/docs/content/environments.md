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

The five runnable projects — Identity, Platform, Master, Accounting and the Gateway — each carry five files:

```
Identity.Api/
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
Host=localhost;Port=5432;Database=retailerp_master;Username=postgres;Password=123
```

Clone, `createdb`, press **F5**. Nothing to set up by hand. The launch configurations in `.vscode/launch.json` all set `ASPNETCORE_ENVIRONMENT=Development`, so this is what you get unless you go out of your way.

### Everything else ships blank

Staging, UAT and Production carry the same keys with **empty strings**. They are supplied at run time from the environment.

> **Blank rather than absent is deliberate.** A key that is missing falls back to whatever the layer below it says — which for a hand-rolled default means a localhost connection string compiled into the binary, quietly connecting Production to nothing. An empty value fails at startup instead. Loud beats silent.

### Supplying values

ASP.NET Core maps environment variables onto configuration keys by replacing `:` with a **double underscore**, which is what container runtimes and app services accept.

| Configuration key | Environment variable | Set for |
|---|---|---|
| `ConnectionStrings:MasterDatabase` | `ConnectionStrings__MasterDatabase` | Identity, Platform, Master |
| `ConnectionStrings:DesignTimeDatabase` | `ConnectionStrings__DesignTimeDatabase` | Accounting |
| `ConnectionStrings:TenantFallback` | `ConnectionStrings__TenantFallback` | Accounting |
| `TenantDatabase:ConnectionTemplate` | `TenantDatabase__ConnectionTemplate` | Platform |
| `Jwt:SigningKey` | `Jwt__SigningKey` | Identity, Accounting |
| `Encryption:Key` | `Encryption__Key` | Platform |
| `Platform:BaseUrl` | `Platform__BaseUrl` | Identity, Accounting |
| `Identity:BaseUrl` | `Identity__BaseUrl` | Platform |
| `Master:BaseUrl` | `Master__BaseUrl` | Platform |
| `App:BaseUrl` | `App__BaseUrl` | Identity |

The Gateway's destinations follow the same rule, one level deeper — a cluster id and a destination id sit in the path:

```
ReverseProxy__Clusters__identity__Destinations__d1__Address
ReverseProxy__Clusters__platform__Destinations__d1__Address
ReverseProxy__Clusters__master__Destinations__d1__Address
ReverseProxy__Clusters__accounting__Destinations__d1__Address
```

The **routes** are not environment-dependent and stay in `appsettings.json`. Only where each cluster points changes between environments.

`Jwt:Issuer`, `Jwt:Audience` and the token lifetimes are non-secret product decisions, so they live in `appsettings.json` and are the same everywhere. `Jwt__SigningKey` must be **identical across Identity and Accounting** in a given environment — Identity issues the token, Accounting validates it.

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

The eight backend services that are not yet implemented — Contacts, Crm, Inventory, Sales, Purchase, Banking, Support, Reporting — have only a placeholder `appsettings.json`. Per-environment files arrive with the service.
