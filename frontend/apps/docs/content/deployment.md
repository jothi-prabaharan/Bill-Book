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

## The databases are made by the installation, not the app

Master creates a missing database only on a developer's machine, in the `Development` environment. Everywhere else the databases already exist before Master starts:

- **On Azure**, `EP_Admin` and `IN000001` are declared in `deploy/azure/main.bicep` and created by the deployment.
- **On your own PCs**, the database container creates both the first time it starts with an empty data volume, from `deploy/local/db/init`.

If one is missing, Master stops at startup and names the database, the server and where it should have been created. The application's database login therefore never needs the right to create databases.

### More customers: standby databases

A tenant database holds up to 100 customers (`Sharding:CustomersPerPool`). An Elite customer gets a database of its own. When every database is full, or an Elite customer signs up, Master takes the next **standby database**. It migrates every tenant schema into it, registers it, and places the customer there. Standby databases are made by the installation, like the first one, and listed in `Sharding:StandbyDatabases` (on Azure, `Sharding__StandbyDatabases__0`, `__1`, …). Keep one or two spare. With none left, signup answers *service unavailable* and Master logs that a database is needed. On a developer's machine Master creates the next one itself (`IN000002`, …). At every start, Master migrates every registered database and recounts the customers in each.

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
| Uploads and archived documents | A folder on the PC's disk, or an SFTP file server — the same layout either way |
| The web, portal, admin and docs apps | One web server, one port each |
| The public address | Your domain name, through a Cloudflare Tunnel |

It also runs **split across PCs**, one part each: the web apps, the gateway, the
services, the stock costing worker and the database, with uploaded files on a
sixth PC running the SFTP server built into Windows. The setup script then writes
one settings file per PC, holding only what that PC needs — the web PC, the one
facing the internet, is never given the database password or the signing keys.
Traffic between the PCs is not encrypted, so they belong on the office's own
wired network; the public side is always HTTPS.

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

## Installing the web app on a PC

Chrome and Edge offer to install the web app — an **Install** button at the right
of the address bar — wherever it is served over HTTPS, or from `localhost` on the
PC running it. The installed app opens in its own window with its own taskbar
icon, and is the same site underneath: nothing is stored on the PC, and every
deployment reaches it on its next start. It has no offline mode, by design —
every screen reads live books.

## What is not set up yet

- Monitoring and alerts
- Custom domain names on Azure — everything there serves on its default address
- A staging environment separate from production
- A web application firewall in front of the gateway

## Email delivery

Invitations, one-time codes and password resets are sent by Master itself unless you switch on the notification worker. Master queues each email in memory and sends it on a background thread through the customer's mailbox from **Settings › Email**, or through the platform's default mailbox. A restart loses anything still queued. That is acceptable for these messages, because each can be requested again.

To have `Notification.Worker` deliver email instead, so nothing queued is lost on a restart, all four of these must be in place:

1. An Azure Service Bus namespace with an `EmailRequested` topic (duplicate detection on) and a subscription named `notification-worker`, or whatever `ServiceBus:EmailSubscription` says.
2. **Master** has `ServiceBus:Namespace` set and `Notification:EmailWorker` set to `true`. Service Bus alone is not enough: that way a namespace with no worker behind it cannot swallow every email.
3. **The worker** has the same `ServiceBus:Namespace`, and has `ConnectionStrings:TenantDatabase` and `Master:BaseUrl`. Its identity needs *Azure Service Bus Data Receiver* on the subscription, and the internal key that Master's `internal/smtp/resolved` route requires.
4. The worker creates its `ntf` schema on start. It records each message it sends, so a message the broker delivers twice goes out once.

Neither deployment in this repository runs the worker yet, so both keep the in-process path.

## Rate sync worker

`RateSync.Worker` fills the `rat` schema in the master database. For now that means only the RBI reference rates (TK-26). It needs:

| Setting | Meaning |
|---|---|
| `ConnectionStrings:AdminDatabase` | The master database, the same one Master uses |
| `Rbi:ReferenceRateUrl` | The page the reference rates are read from. Defaults to `https://www.rbi.org.in/` |
| `RateSync:RbiAfter` | The time in India after which the day's rates are fetched. Defaults to `13:45` |
| `RateSync:CheckIntervalMinutes` | How often it wakes to check. A failed day is retried at the next wake. Defaults to 60 |

The worker needs outbound HTTPS to the RBI host. It is not in the Azure or single-PC deployment yet. Its parser is checked against an imitation of the page, and it will be deployed after it has been checked against a real one.

