# Deploying to Azure

Eight APIs and a gateway on Container Apps, an always-on costing worker beside
them, PostgreSQL Flexible Server with no public address, and the four web apps on
Static Web Apps. Secrets, files and events go to Key Vault, Blob Storage and
Service Bus, all reached by managed identity — there is no connection string with
a key in it anywhere in the deployment.

`main.bicep` is the infrastructure; `.github/workflows/deploy-azure.yml` is the
pipeline. This file is how to set them up and what to know before they surprise
you.

> **The first deploy will stop at step 3 until one thing on `main` is fixed.**
> `AdminDbContext` has drifted from its migrations — the `mst.Menus` and
> `mst.MenuPermissions` seed data was changed after the admin migration was
> squashed — so Master refuses to start on any fresh database, on any host.
> An incremental migration does **not** fix it: EF applies the 379 row updates
> one at a time and collides on `IX_MenuPermissions_MenuId_PermissionCode`
> midway. Re-squashing the admin migration does; that was verified locally and
> then reverted, because the menu seed is owned by work in progress elsewhere.
> The migration job is doing exactly its job by failing here.

---

## What gets created

| | Count | |
|---|---|---|
| Container Apps | 10 | 8 APIs (internal), gateway (external), costing worker (no ingress) |
| Container Apps job | 1 | `migrate` — manual trigger, run once per deploy |
| PostgreSQL Flexible Server | 1 | v16, Burstable B2ms, VNet-integrated, `EP_Admin` + `IN000001` |
| Key Vault | 1 | RBAC mode, five secrets, purge protection on |
| Storage account | 1 | ZRS, shared-key access **off**, versioning and 14-day soft delete |
| Service Bus namespace | 1 | Standard (topics need it), local auth **off**, one topic per event |
| Container Registry | 1 | Basic, admin user off |
| Static Web Apps | 4 | Free tier: web, portal, admin, docs |
| Managed identities | 11 | one per workload |
| VNet, private DNS zone, Log Analytics | 1 each | |

`Notification.Worker` and `RateSync.Worker` are not deployed — each is a
`.csproj` and an empty `Consumers/` folder, so a deployed one would run a host
that does nothing.

**Printing is deployed but has no gateway route.** Its callers push payloads to it
service-to-service, which is the design in `docs/Printing.md`. Exposing it through
the gateway means adding it to `gatewayClusters` in `main.bicep` alongside its
routes in the gateway's `appsettings.json`.

---

## One-time setup

### 1. Resource group and providers

```bash
az group create --name rg-billbook-prod --location centralindia

for p in Microsoft.App Microsoft.ContainerRegistry Microsoft.DBforPostgreSQL \
         Microsoft.KeyVault Microsoft.ServiceBus Microsoft.Web Microsoft.Storage \
         Microsoft.OperationalInsights Microsoft.Network Microsoft.ManagedIdentity; do
  az provider register --namespace "$p"
done
```

One resource group per environment. Resource names are derived from the group,
so staging and production never collide.

### 2. A federated identity for the pipeline

The workflow signs in with OIDC — no client secret exists to leak or expire.

```bash
app=$(az ad app create --display-name billbook-deploy --query appId -o tsv)
az ad sp create --id "$app"

# Trusts exactly this repository's "production" environment, nothing else.
az ad app federated-credential create --id "$app" --parameters '{
  "name": "github-production",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:<owner>/<repo>:environment:production",
  "audiences": ["api://AzureADTokenExchange"]
}'

# Owner on the resource group, because the template creates role assignments.
# Contributor plus "Role Based Access Control Administrator" is the narrower
# alternative if Owner is more than your policy allows.
az role assignment create --assignee "$app" --role Owner \
  --scope "$(az group show --name rg-billbook-prod --query id -o tsv)"
```

### 3. GitHub settings

Create an environment named **`production`** (Settings → Environments). Adding a
required reviewer there puts a human approval in front of every deploy.

**Repository variables:**

| | |
|---|---|
| `AZURE_CLIENT_ID` | the app id from step 2 |
| `AZURE_TENANT_ID` | `az account show --query tenantId -o tsv` |
| `AZURE_SUBSCRIPTION_ID` | `az account show --query id -o tsv` |
| `AZURE_RESOURCE_GROUP` | `rg-billbook-prod` |
| `BOOTSTRAP_OWNER_EMAIL` | the first account's address, or leave unset |

**Environment secrets** (on `production`), generated once and kept:

```bash
# Upper, lower and digits: Azure requires three character classes, and base64
# stripped of /+= gives exactly those — without the semicolon that would cut a
# connection string short.
openssl rand -base64 36 | tr -d '/+=\n'      # POSTGRES_ADMIN_PASSWORD
openssl rand -base64 48 | tr -d '\n'          # JWT_SIGNING_KEY
openssl rand -hex 32                          # INTERNAL_API_KEY
openssl rand -base64 32                       # ENCRYPTION_KEY  (must be exactly 32 bytes)
```

Every deploy passes these to Bicep, which writes them into Key Vault. **Changing
one in GitHub and redeploying rotates it.** Changing the JWT key signs every user
out; changing the encryption key makes stored SMTP passwords unreadable.

---

## Deploying

Actions → **Deploy — Azure** → type the resource group name to confirm.

| Step | What | If it fails |
|---|---|---|
| 1 | Bicep, `deployApps=false`: infrastructure and the migration job | Nothing changed that serves traffic |
| 2 | Build and push ten images tagged with the commit SHA | Same |
| 3 | Run `migrate` and wait | Same — the running version is untouched |
| 4 | Bicep, `deployApps=true`: every app rolls to the new image | The failed app's previous revision keeps serving |
| 5 | Build the SPAs, write `config.js`, upload to Static Web Apps | Backend is already live on the new version |

The same five steps run on the first deploy and every one after it.

**On a first deploy, step 1 or 4 may fail once on a role assignment.** Azure can
take a few minutes to propagate a new identity's permissions, and an app that
tries to pull its image in that window is refused. Re-run the workflow; it is
idempotent.

---

## Things worth knowing

**`AZURE_CLIENT_ID` is set on every container, and must be.** The apps run as
user-assigned identities — system-assigned ones do not exist until the app does,
so could not hold `AcrPull` in time for the first image pull. With a user-assigned
identity, `DefaultAzureCredential` finds it only through that variable; without
it, it looks for a system identity, finds none, and fails at the first secret
read rather than at startup.

**Migrations run as a job, then again as no-ops.** Every service migrates its own
schema on startup. Left to that alone, a rollout starts eight services racing to
migrate the same database. The job — Master's image with
`Migrations:ExitWhenDone=true` — migrates everything first, exits 0, and the
services then find nothing to do.

**Every service gets every inter-service URL.** Each has a different set of
required ones, and several ship `localhost` defaults in `appsettings.json`. In
production a localhost default is worse than a missing setting — it fails by
quietly calling nothing. Setting all eight everywhere removes that class of
mistake rather than auditing for it.

**The web apps find the gateway through `config.js`, not a rebuild.** Step 5
writes `window.__BB_CONFIG__ = { apiBaseUrl: … }` into each app, and the gateway's
CORS allows exactly the three Static Web App origins. Serving them from a custom
domain means adding that origin to `extraAllowedOrigins`, or every API call from
it is blocked by the browser.

**The costing worker is always on.** `inv.StockMovements` is its queue: it claims
movements with a guarded update and costs them in order. At zero replicas it would
stop claiming the moment traffic stopped. At two it would only add contention,
because the guarded claim means throughput is not the constraint.

**Connections are the first thing to run out.** Every replica opens a pool per
shard. Check `max_connections` for the Postgres SKU against
replicas × shards × pool size before raising replica counts, and cap
`Maximum Pool Size` in the connection string or add PgBouncer (built into Flexible
Server) before moving up a tier.

**Download links are signed without a key.** The storage account refuses
shared-key access outright, so `AzureBlobFileStorage` signs with a user delegation
key issued to the app's identity instead. That needs Storage Blob Data
Contributor, which Master and Sales hold.

**Static Web Apps live in East Asia.** They are not offered in India. Only the
control plane lives there; content is served from Microsoft's global edge.

---

## Not covered yet

- **Monitoring and alerts.** Logs flow to Log Analytics; nothing alerts on them.
- **Custom domains.** Everything serves on its default host name.
- **A WAF or Front Door** in front of the gateway.
- **Private endpoints** for Key Vault, Storage and Service Bus. They are reached
  over their public endpoints with Entra ID auth and no keys enabled; private
  endpoints would take them off the internet entirely.
- **A staging environment.** Same template, a second resource group and a second
  GitHub environment.
