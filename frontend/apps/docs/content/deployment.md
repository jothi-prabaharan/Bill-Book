# Deployment

Where the product runs, and what an operator has to create before it will.

The full runbook — every command, every variable, every trap — is in
`deploy/gcp/README.md` beside the Terraform. This page is the shape of the thing.

## Nothing in the application is tied to one cloud

Three pieces of infrastructure sit behind interfaces: somewhere to keep secrets,
somewhere to put uploaded files, and something to publish events onto. Each has
three implementations — one for Google Cloud, one for Azure, one for a developer's
own machine — and which one runs is decided at startup from configuration rather
than at compile time. Every build carries all of them.

A service refuses to start in production if it cannot find a real secret store.
That is deliberate: a service that started anyway would serve requests reading
credentials out of whatever configuration happened to be lying around, and the
first sign of it would be a credential somewhere nobody meant to put one.

## On Google Cloud

| What | Where it runs |
|---|---|
| The seven service APIs | Cloud Run, internal only |
| The gateway | Cloud Run, public — the single front door |
| Stock costing | Cloud Run, always on |
| The database | Cloud SQL for PostgreSQL 16, private, no public address |
| Uploads and archived documents | Cloud Storage |
| Secrets | Secret Manager |
| Events | Pub/Sub |
| The web, portal, admin and docs apps | Firebase Hosting |

Only two things are reachable from the internet: the gateway and the web apps.
The seven APIs are not, and the database has no public address at all.

The default region is Mumbai (`asia-south1`). This is an Indian GST product and
the books belong to Indian businesses, so the data stays in India and the latency
that matters is to Indian users. Delhi (`asia-south2`) is the alternative. It is
a decision to make once — moving afterwards replaces the database instance.

## Stock costing runs on its own

Costing is the one part of the system that is not driven by someone using it.
Stock movements queue up in the database as they happen, and a separate worker
walks them in order and works out what each one cost.

That worker is kept running permanently rather than started on demand. If it were
allowed to sleep when nobody was using the product, movements would sit uncosted
until the next person happened to sign in — and the first anybody would know of
it is a margin figure being wrong at month end.

## Upgrades apply the database changes once

Each deployment runs the database migration as a single separate step, waits for
it to finish, and only then moves traffic onto the new version. If the migration
fails, the deployment stops there and the running version is untouched.

## What is not set up yet

- A staging environment separate from production
- Monitoring, uptime checks and alerts
- Custom domain names — everything serves on its default URL
- Budget and spending limits
