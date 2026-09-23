# Deployment

Where the product runs, and what an operator has to create before it will.

The full runbook — every command, every setting, every trap — is in
`deploy/azure/README.md` beside the Bicep templates. This page is the shape of the
thing.

## Where each part runs

| What | Where it runs |
|---|---|
| The eight service APIs | Azure Container Apps, internal only |
| The gateway | Azure Container Apps, public — the single front door |
| Stock costing | Azure Container Apps, always on |
| The database | Azure Database for PostgreSQL, private, no public address |
| Uploads and archived documents | Azure Blob Storage |
| Secrets | Azure Key Vault |
| Events | Azure Service Bus |
| The web, portal, admin and docs apps | Azure Static Web Apps |

Only two things are reachable from the internet: the gateway and the web apps. The
eight services are not, and the database has no public address at all.

Nothing holds a password or key to reach the vault, the file store or the event
bus. Each service runs under its own managed identity, and those three services
are configured to refuse key-based access outright — so there is no such key to
leak.

The default region is Central India (Pune). This is an Indian GST product and the
books belong to Indian businesses, so the data stays in India and the latency that
matters is to Indian users. South India (Chennai) is the alternative. It is a
decision to make once — moving afterwards replaces the database.

## A service refuses to start without a vault

A service running in production looks for Key Vault before anything else, and
stops with a message naming the setting if it cannot find one. That is
deliberate: a service that started anyway would serve requests reading
credentials out of whatever configuration happened to be lying around, and the
first sign of it would be a credential somewhere nobody meant to put one.

## Upgrades apply the database changes once, first

Each deployment runs the database migration as a single separate step, waits for
it to finish, and only then moves the services onto the new version. If the
migration fails, the deployment stops there and the running version is untouched.

## Stock costing runs on its own

Costing is the one part of the system that is not driven by someone using it.
Stock movements queue up in the database as they happen, and a separate worker
walks them in order and works out what each one cost.

That worker is kept running permanently. If it were allowed to sleep when nobody
was using the product, movements would sit uncosted until the next person happened
to sign in — and the first anybody would know of it is a margin figure being wrong
at month end.

## The web apps are told where the API is when they are deployed

The same build of each web app runs in every environment. What differs is one
small settings file, written at deployment, that names the gateway's address — so
moving the API, or adding a staging environment, needs no rebuild.

## What is not set up yet

- Monitoring and alerts
- Custom domain names — everything serves on its default address
- A staging environment separate from production
- A web application firewall in front of the gateway
