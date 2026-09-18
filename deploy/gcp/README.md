# Deploying to Google Cloud

What this is, in one line: seven APIs and a gateway on Cloud Run, an always-on
costing worker beside them, PostgreSQL on Cloud SQL, and the four web apps on
Firebase Hosting.

Nothing here is Google-specific in the application. The three infrastructure
interfaces — `ISecretStore`, `IFileStorage`, `IEventPublisher` — each have a
Google implementation beside their Azure and local ones, and which is registered
is decided at startup from configuration. **Setting `Gcp:ProjectId` is what
selects all three.**

---

## What gets created

| | Count | |
|---|---|---|
| Cloud Run services | 8 | 7 APIs (internal) + gateway (public) |
| Cloud Run service | 1 | `costing-worker`, always on |
| Cloud Run job | 1 | `migrate` |
| Cloud SQL instance | 1 | PostgreSQL 16, private IP, holding `EP_Admin` and `IN000001` |
| Artifact Registry | 1 | image repository |
| Cloud Storage bucket | 1 | uploads and the document archive |
| Secret Manager secrets | 5 | |
| Pub/Sub topics | 1 | one per event type |
| Service accounts | 10 | one per service |

`Notification.Worker` and `RateSync.Worker` are deliberately not deployed. They
are a `.csproj` and an empty `Consumers/` folder; deploying them would run a host
that does nothing.

**There is no VPC connector and none is needed.** Cloud Run reaches Cloud SQL
through the built-in connector, which mounts a unix socket into the container —
which is why the connection strings in Secret Manager are
`Host=/cloudsql/<connection name>` rather than an IP.

---

## First deploy

### 1. Infrastructure

```bash
cd deploy/gcp
cp terraform.tfvars.example terraform.tfvars   # fill in project_id and image_tag
terraform init
terraform apply
```

`image_tag` must name images that already exist, so on the very first run either
push one set of images by hand first, or apply, let the Cloud Run resources fail
their first revision, and re-apply after the build workflow has run. The second
is normal and harmless.

**Use a remote backend.** The state file contains the generated database
password, the JWT signing key and the internal API key in clear, as every
Terraform-generated credential is. A GCS bucket with versioning and restricted
IAM, not a file on somebody's laptop.

### 2. Workload Identity Federation

The deploy workflow authenticates with no service-account key at all. Create the
pool and provider once, then set these repository variables:

- `GCP_PROJECT_ID`
- `GCP_WORKLOAD_IDENTITY_PROVIDER` — the full
  `projects/…/locations/global/workloadIdentityPools/…/providers/…` resource name
- `GCP_DEPLOY_SERVICE_ACCOUNT`

The deploy service account needs `roles/run.admin`,
`roles/artifactregistry.writer`, `roles/iam.serviceAccountUser` and
`roles/firebasehosting.admin`.

### 3. Hosting sites

```bash
firebase hosting:sites:create <project>-web      # and portal, admin, docs
```

### 4. Deploy

Actions → **Deploy — Google Cloud** → type the project id to confirm.

The workflow builds ten images in parallel, runs the migration job once and waits
for it, then shifts traffic service by service with the gateway last.

---

## Things worth knowing before they bite

**The costing worker must not scale to zero.** `inv.StockMovements` *is* the
queue — the worker claims a movement with a guarded `Pending → InProgress` update
and costs it in order. A scale-to-zero service stops claiming the moment traffic
stops, and movements sit uncosted until something happens to wake it. Hence
`min_instance_count = 1` and `cpu_idle = false`; a polling loop with no inbound
requests gets no CPU at all without the second.

**Migrations run as a job, not on startup.** `DatabaseMigrationService` also
migrates when Master starts, which is fine for one process and wrong for several:
four cold-starting instances means four racing to apply the same migration. The
job makes it deterministic and makes a failure stop the deploy. This is the
sharper form of the open question in `CLAUDE.md` about whether
auto-create-on-missing belongs in a production startup path at all.

**Connection count is the first thing that will break under load.** Cloud Run
scales horizontally and `TenantDatabaseResolver` opens a pool per shard, so the
total is instances × shards × pool size. `max_connections` is set to 200. When
"too many clients" appears, cap `Maximum Pool Size` in the connection strings or
put PgBouncer in front — raising the tier treats the symptom.

**Signed download URLs need one IAM grant.** `GcsFileStorage.GetDownloadUrlAsync`
signs through the IAM `signBlob` API, which needs
`roles/iam.serviceAccountTokenCreator` on the service account over itself. The
Terraform grants it. Without it the method returns null and every download
streams through the API instead — which works, and is exactly the kind of quiet
degradation nobody notices, so check it deliberately after the first deploy.

**The gateway's routes are environment variables, not `appsettings.Production.json`.**
A Cloud Run URL is not knowable until the service exists. The committed file
keeps its localhost defaults for a local run and Terraform overrides them with
`ReverseProxy__Clusters__<name>__Destinations__d1__Address`, which is what
ASP.NET Core configuration precedence is for. Editing the JSON to hardcode Cloud
Run URLs would break running it locally and gain nothing.

**Events deliver for the first time here.** Every previous deployment target had
`LoggingEventPublisher`, which logs and drops. Pub/Sub is at-least-once, so
**every consumer must dedupe on the `eventId` attribute** — that is not a
Pub/Sub caveat, it was true of the Service Bus design that was never written.

---

## Region

`asia-south1` (Mumbai) is the default because this is an Indian GST product and
the data is Indian businesses' books. `asia-south2` is Delhi. Changing it after
the first apply replaces the Cloud SQL instance, so decide once.

---

## What this does not cover

- **A staging environment.** One `terraform.tfvars` per environment in separate
  workspaces is the intended shape; only one is written.
- **Monitoring and alerting.** Nothing here creates an uptime check or an alert
  policy.
- **Custom domains.** Firebase Hosting and the gateway both serve their default
  URLs.
- **Cost controls.** No budget or quota is set.
