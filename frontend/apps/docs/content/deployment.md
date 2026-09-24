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

## How stored files are organised

Uploaded attachments and archived documents are filed by customer first, then by
branch, then by product and module — for example, a branch's archived sales
invoices sit together under the customer's code, the branch, `retail-erp` and
`sales`. So everything belonging to one customer is in one place, which is what
an export on request or a clean-up on leaving needs.

A file is never overwritten by accident: saving a second file where one already
exists is refused, and the earlier file is left exactly as it was.

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

## Running on your own PC instead

The same product runs on a single Windows 11 PC under Docker Desktop, for a
business that would rather keep its books on its own machine than pay for a
cloud subscription. The steps are in `deploy/local/README.md`.

| What | Where it runs on the PC |
|---|---|
| The services, the gateway and stock costing | Containers, built from the same images as Azure |
| The database | PostgreSQL in a container, on the PC's own disk |
| Uploads and archived documents | A folder on the PC's disk, with the same layout |
| The web, portal, admin and docs apps | One web server, one port each |
| The public address | Your domain name, through a Cloudflare Tunnel |

**No static IP is needed.** The PC opens an outgoing connection to Cloudflare, and
visitors reach the site through it — so nothing on the PC is exposed to the
internet, the home address is never published, and it works behind the shared
addresses most Indian home broadband uses, where a static-IP setup cannot.
Cloudflare provides the HTTPS certificate.

The setup script creates the business, its first branch and the owner's
account, with the branch's chart of accounts, GST rates and numbering loaded on
first start. The public free-trial sign-up does not work on a new installation
yet, on a PC or on Azure: making room for trial businesses is still to be built.

What the PC takes on that Azure would have done:

- **It must stay on.** The site is down whenever the PC is off, asleep or
  offline.
- **Backups are yours.** A nightly script dumps every database and every
  uploaded file, but onto the same disk until you copy them somewhere else.
- **Secrets live in one file**, written by the setup script. Losing it means a
  backup cannot be restored.

## What is not set up yet

- Monitoring and alerts
- Custom domain names on Azure — everything there serves on its default address
- A staging environment separate from production
- A web application firewall in front of the gateway
