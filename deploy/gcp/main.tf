# Foundation: APIs, registry, database, bucket, topics and secrets.
# Cloud Run itself is in cloud_run.tf.

locals {
  # The seven services, their project paths and their assembly names. The image
  # build in .github/workflows/deploy.yml reads the same three facts, so if a
  # service is added it is added in both places — there is no way to derive one
  # from the other without a convention that the folder layout does not have
  # (Api/Master/Master.Api is not Api/master/master).
  services = {
    master     = { path = "Api/Master/Master.Api", assembly = "Master.Api" }
    accounting = { path = "Api/Accounting/Accounting.Api", assembly = "Accounting.Api" }
    inventory  = { path = "Api/Inventory/Inventory.Api", assembly = "Inventory.Api" }
    sales      = { path = "Api/Sales/Sales.Api", assembly = "Sales.Api" }
    purchase   = { path = "Api/Purchase/Purchase.Api", assembly = "Purchase.Api" }
    customer   = { path = "Api/Customer/Customer.Api", assembly = "Customer.Api" }
    reporting  = { path = "Api/Reporting/Reporting.Api", assembly = "Reporting.Api" }
  }

  # The event types published today. One topic each — a single topic carrying
  # everything would make every consumer read and discard most of what it got.
  #
  # Only CustomerProvisioned is published so far, and PubSubEventPublisher will
  # create a topic it does not find, so a new event type is not blocked on this
  # list. It is here so the topics exist before anything publishes to them and so
  # their IAM is declared rather than inherited.
  topics = ["CustomerProvisioned"]

  registry = "${var.region}-docker.pkg.dev/${var.project_id}/${google_artifact_registry_repository.services.repository_id}"
}

# ---------------------------------------------------------------------------
# APIs
# ---------------------------------------------------------------------------
#
# Enabled explicitly rather than by hand in the console, so a fresh project is
# one `terraform apply` from working. disable_on_destroy is off throughout: a
# destroy of this workspace should not turn off an API another workspace in the
# same project is using.
resource "google_project_service" "required" {
  for_each = toset([
    "run.googleapis.com",
    "sqladmin.googleapis.com",
    "secretmanager.googleapis.com",
    "artifactregistry.googleapis.com",
    "pubsub.googleapis.com",
    "storage.googleapis.com",
    "cloudbuild.googleapis.com",
    "iamcredentials.googleapis.com",
    "compute.googleapis.com",
  ])

  service            = each.value
  disable_on_destroy = false
}

# ---------------------------------------------------------------------------
# Artifact Registry
# ---------------------------------------------------------------------------
resource "google_artifact_registry_repository" "services" {
  location      = var.region
  repository_id = "bill-book"
  format        = "DOCKER"
  description   = "Backend service images, one per API, gateway and worker."

  depends_on = [google_project_service.required]
}

# ---------------------------------------------------------------------------
# Cloud SQL
# ---------------------------------------------------------------------------
#
# One instance holding both databases: EP_Admin (the shared master database) and
# IN000001 (the first tenant shard). That is the tenancy model the application
# already has — mst.Customers.DatabaseName names the shard, and
# ITenantDatabaseResolver reads it per request — rather than anything introduced
# here. A second shard is a second google_sql_database, not a second instance.
resource "google_sql_database_instance" "main" {
  name             = "bill-book"
  database_version = "POSTGRES_16"
  region           = var.region

  deletion_protection = var.sql_deletion_protection

  settings {
    tier              = var.sql_tier
    availability_type = "ZONAL"
    disk_type         = "PD_SSD"
    disk_size         = 20
    disk_autoresize   = true

    ip_configuration {
      # No public IP. Cloud Run reaches this over the built-in Cloud SQL
      # connector (a unix socket in the container), which needs no VPC connector
      # and no address reachable from the internet.
      ipv4_enabled = false
      # Private Service Access into the default network. The producer peering
      # this needs is created below.
      private_network = data.google_compute_network.default.id
    }

    backup_configuration {
      enabled                        = true
      start_time                     = "18:30" # 00:00 IST
      point_in_time_recovery_enabled = true
      transaction_log_retention_days = 7
    }

    database_flags {
      # The application opens a pool per shard per instance, and Cloud Run scales
      # horizontally, so the connection count is instances x shards x pool size.
      # 200 is headroom for the shape this starts at; it is also the number to
      # revisit first when "too many clients" appears, ahead of raising the tier.
      name  = "max_connections"
      value = "200"
    }
  }

  depends_on = [
    google_project_service.required,
    google_service_networking_connection.private_vpc,
  ]
}

data "google_compute_network" "default" {
  name = "default"

  depends_on = [google_project_service.required]
}

# The address range Cloud SQL's private IP is allocated from, and the peering
# that makes it reachable. Both are one-time plumbing for a private instance.
resource "google_compute_global_address" "private_ip" {
  name          = "bill-book-sql-private-ip"
  purpose       = "VPC_PEERING"
  address_type  = "INTERNAL"
  prefix_length = 16
  network       = data.google_compute_network.default.id
}

resource "google_service_networking_connection" "private_vpc" {
  network                 = data.google_compute_network.default.id
  service                 = "servicenetworking.googleapis.com"
  reserved_peering_ranges = [google_compute_global_address.private_ip.name]
}

# The shared master database: users, roles, customers, licences, the shard
# registry. Named to match the application's own default.
resource "google_sql_database" "admin" {
  name     = "EP_Admin"
  instance = google_sql_database_instance.main.name
}

# The first tenant shard. DatabaseMigrationService provisions IN000001 on startup
# if it is missing, but declaring it means the migration job below finds it
# already there rather than racing to create it.
resource "google_sql_database" "tenant" {
  name     = "IN000001"
  instance = google_sql_database_instance.main.name
}

resource "google_sql_user" "app" {
  name     = "billbook"
  instance = google_sql_database_instance.main.name
  password = random_password.sql.result
}

resource "random_password" "sql" {
  length = 32
  # Excludes characters that need escaping in a Npgsql connection string. A
  # password containing a semicolon truncates the string it is in, and the
  # failure reads as a wrong password rather than as a quoting bug.
  override_special = "-_.~"
  special          = true
}

# ---------------------------------------------------------------------------
# Cloud Storage
# ---------------------------------------------------------------------------
resource "google_storage_bucket" "documents" {
  # Bucket names are global across all of Google Cloud, so this is qualified with
  # the project id — the same name GcsFileStorage's registration composes when
  # Storage:Bucket is not set explicitly.
  name     = "${var.project_id}-documents"
  location = var.region

  # Object ACLs off. Every read goes through a signed URL or the API, and a
  # per-object ACL is the mechanism by which one of these documents would end up
  # world-readable by accident.
  uniform_bucket_level_access = true
  public_access_prevention    = "enforced"

  versioning {
    # These are customers' GST certificates and archived invoices. An overwrite
    # is recoverable; without this it is not.
    enabled = true
  }

  depends_on = [google_project_service.required]
}

# ---------------------------------------------------------------------------
# Pub/Sub
# ---------------------------------------------------------------------------
resource "google_pubsub_topic" "events" {
  for_each = toset(local.topics)
  name     = each.value

  depends_on = [google_project_service.required]
}

# ---------------------------------------------------------------------------
# Secrets
# ---------------------------------------------------------------------------
#
# Values are not in Terraform except the two it generates, and those are marked
# sensitive so they do not print. Everything else is created empty and filled by
# an operator with `gcloud secrets versions add`, because a secret in a state
# file is a secret in whatever holds the state file.
resource "google_secret_manager_secret" "app" {
  for_each = toset([
    "jwt-signing-key",
    "internal-api-key",
    "encryption-key",
    "admin-db-connection",
    "tenant-db-connection",
  ])

  secret_id = each.value

  replication {
    user_managed {
      replicas {
        # Pinned to the deployment's own region rather than replicated
        # automatically: this is an Indian product and the secrets are as much
        # customer data as anything else in it.
        location = var.region
      }
    }
  }

  depends_on = [google_project_service.required]
}

# The two connection strings are generated rather than operator-supplied, because
# Terraform is what knows the instance name and the password it just made.
#
# Host is the unix socket Cloud Run's Cloud SQL connector mounts. Npgsql treats a
# Host beginning with "/" as a socket directory, which is what makes this work
# without a VPC connector or a proxy sidecar.
resource "google_secret_manager_secret_version" "admin_db" {
  secret = google_secret_manager_secret.app["admin-db-connection"].id
  secret_data = join(";", [
    "Host=/cloudsql/${google_sql_database_instance.main.connection_name}",
    "Database=${google_sql_database.admin.name}",
    "Username=${google_sql_user.app.name}",
    "Password=${random_password.sql.result}",
  ])
}

resource "google_secret_manager_secret_version" "tenant_db" {
  secret = google_secret_manager_secret.app["tenant-db-connection"].id
  secret_data = join(";", [
    "Host=/cloudsql/${google_sql_database_instance.main.connection_name}",
    "Database=${google_sql_database.tenant.name}",
    "Username=${google_sql_user.app.name}",
    "Password=${random_password.sql.result}",
  ])
}

# JWT signing key and the internal API key are generated here because nothing
# else needs to know them — no human types either, and both must differ per
# environment. A shared signing key means a token minted in staging is accepted
# in production.
resource "random_password" "jwt" {
  length  = 64
  special = false
}

resource "random_password" "internal_key" {
  length  = 48
  special = false
}

resource "google_secret_manager_secret_version" "jwt" {
  secret      = google_secret_manager_secret.app["jwt-signing-key"].id
  secret_data = random_password.jwt.result
}

resource "google_secret_manager_secret_version" "internal_key" {
  secret      = google_secret_manager_secret.app["internal-api-key"].id
  secret_data = random_password.internal_key.result
}

resource "random_password" "encryption" {
  length  = 44
  special = false
}

resource "google_secret_manager_secret_version" "encryption" {
  secret      = google_secret_manager_secret.app["encryption-key"].id
  secret_data = random_password.encryption.result
}
