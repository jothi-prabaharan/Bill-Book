# Cloud Run: seven internal APIs, one public gateway, one always-on worker, and
# a job that migrates.

locals {
  # Every service gets these. Gcp:ProjectId is the one that matters most: it is
  # what makes Shared.Kernel pick Secret Manager, Cloud Storage and Pub/Sub over
  # their Azure and local counterparts, and without it a Production start fails
  # deliberately rather than falling back to configuration.
  common_env = {
    ASPNETCORE_ENVIRONMENT = "Production"
    Gcp__ProjectId         = var.project_id
    Storage__Bucket        = google_storage_bucket.documents.name
  }

  common_secret_env = {
    ConnectionStrings__AdminDatabase  = "admin-db-connection"
    ConnectionStrings__TenantDatabase = "tenant-db-connection"
    Jwt__SigningKey                   = "jwt-signing-key"
    Internal__ApiKey                  = "internal-api-key"
    Encryption__Key                   = "encryption-key"
  }
}

# ---------------------------------------------------------------------------
# Service accounts
# ---------------------------------------------------------------------------
#
# One per service rather than one shared, so a compromise of Reporting does not
# carry Master's ability to write secrets. They are cheap; the default compute
# service account, which is what a deployment gets by omission, is Editor on the
# whole project.
resource "google_service_account" "service" {
  for_each = merge(local.services, {
    gateway = { path = "Gateway/Gateway.Api", assembly = "Gateway.Api" }
    costing = { path = "worker/CostingEngine.Worker", assembly = "CostingEngine.Worker" }
    migrate = { path = "Api/Master/Master.Api", assembly = "Master.Api" }
  })

  account_id   = "bb-${each.key}"
  display_name = "Bill Book — ${each.key}"
}

locals {
  # Everything that runs application code and therefore needs the database, the
  # secrets, the bucket and the topics.
  runtime_accounts = merge(
    { for k, v in google_service_account.service : k => v.email },
  )
}

resource "google_project_iam_member" "sql_client" {
  for_each = local.runtime_accounts

  project = var.project_id
  role    = "roles/cloudsql.client"
  member  = "serviceAccount:${each.value}"
}

# Read access to each secret, granted on the secret rather than project-wide, so
# adding a secret does not silently widen what every service can read.
resource "google_secret_manager_secret_iam_member" "accessor" {
  for_each = {
    for pair in setproduct(keys(local.runtime_accounts), keys(google_secret_manager_secret.app)) :
    "${pair[0]}-${pair[1]}" => { account = pair[0], secret = pair[1] }
  }

  secret_id = google_secret_manager_secret.app[each.value.secret].id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${local.runtime_accounts[each.value.account]}"
}

# Master writes a secret (the SMTP-password key) as well as reading them, which
# is why it alone gets the admin role. Every other service would fail closed on a
# write, which is correct — none of them should be writing one.
resource "google_secret_manager_secret_iam_member" "master_writer" {
  for_each = google_secret_manager_secret.app

  secret_id = each.value.id
  role      = "roles/secretmanager.admin"
  member    = "serviceAccount:${google_service_account.service["master"].email}"
}

resource "google_storage_bucket_iam_member" "objects" {
  for_each = local.runtime_accounts

  bucket = google_storage_bucket.documents.name
  role   = "roles/storage.objectAdmin"
  member = "serviceAccount:${each.value}"
}

resource "google_pubsub_topic_iam_member" "publisher" {
  for_each = {
    for pair in setproduct(keys(local.runtime_accounts), local.topics) :
    "${pair[0]}-${pair[1]}" => { account = pair[0], topic = pair[1] }
  }

  topic  = google_pubsub_topic.events[each.value.topic].name
  role   = "roles/pubsub.publisher"
  member = "serviceAccount:${local.runtime_accounts[each.value.account]}"
}

# Lets a service account sign a Cloud Storage V4 URL through the IAM signBlob
# API, over itself. Without this GcsFileStorage.GetDownloadUrlAsync answers null
# and every download streams through the API instead — which works, and is
# exactly the kind of quiet degradation that goes unnoticed, so it is granted
# deliberately rather than left out.
resource "google_service_account_iam_member" "self_sign" {
  for_each = google_service_account.service

  service_account_id = each.value.name
  role               = "roles/iam.serviceAccountTokenCreator"
  member             = "serviceAccount:${each.value.email}"
}

# ---------------------------------------------------------------------------
# The seven APIs
# ---------------------------------------------------------------------------
resource "google_cloud_run_v2_service" "api" {
  for_each = local.services

  name     = each.key
  location = var.region

  # Internal only. These are reached through the gateway, and an API that also
  # answered the internet would be a second front door with none of the
  # gateway's logging in front of it.
  #
  # INTERNAL_ONLY rather than INTERNAL_LOAD_BALANCER: the latter admits only
  # traffic arriving through an internal load balancer, which would reject the
  # direct service-to-service calls this system is built on — Sales posting to
  # Accounting, the gateway proxying anything at all.
  ingress = "INGRESS_TRAFFIC_INTERNAL_ONLY"

  deletion_protection = false

  template {
    service_account = google_service_account.service[each.key].email

    scaling {
      min_instance_count = var.min_instances
      max_instance_count = 10
    }

    containers {
      image = "${local.registry}/${each.key}:${var.image_tag}"

      ports {
        container_port = 8080
      }

      dynamic "env" {
        for_each = local.common_env
        content {
          name  = env.key
          value = env.value
        }
      }

      dynamic "env" {
        for_each = local.common_secret_env
        content {
          name = env.key
          value_source {
            secret_key_ref {
              secret  = google_secret_manager_secret.app[env.value].secret_id
              version = "latest"
            }
          }
        }
      }

      volume_mounts {
        name = "cloudsql"
        # Where Npgsql looks: the connection strings in Secret Manager are built
        # with Host=/cloudsql/<connection name>.
        mount_path = "/cloudsql"
      }

      resources {
        limits = {
          cpu    = "1"
          memory = "1Gi"
        }
      }

      startup_probe {
        # EF Core model building and the first database connection happen at
        # start, and on a cold instance that is not instant. The default
        # four-second budget fails a healthy service; this allows a minute.
        initial_delay_seconds = 10
        period_seconds        = 5
        failure_threshold     = 10
        timeout_seconds       = 3

        tcp_socket {
          port = 8080
        }
      }
    }

    volumes {
      name = "cloudsql"
      cloud_sql_instance {
        instances = [google_sql_database_instance.main.connection_name]
      }
    }
  }

  depends_on = [google_project_service.required]
}

# ---------------------------------------------------------------------------
# Who may call whom
# ---------------------------------------------------------------------------
#
# Cloud Run's internal ingress says "not from the internet"; it does not say
# which callers are allowed. run.invoker is what does, and it is granted along
# the call graph the code actually has rather than to every service uniformly —
# so a service that starts calling one it never called before fails with a 403
# naming both, which is the right way to find out.
resource "google_cloud_run_v2_service_iam_member" "gateway_invokes_api" {
  for_each = local.services

  name     = google_cloud_run_v2_service.api[each.key].name
  location = var.region
  role     = "roles/run.invoker"
  member   = "serviceAccount:${google_service_account.service["gateway"].email}"
}

# The costing worker calls Inventory directly rather than through the gateway.
resource "google_cloud_run_v2_service_iam_member" "costing_invokes_inventory" {
  name     = google_cloud_run_v2_service.api["inventory"].name
  location = var.region
  role     = "roles/run.invoker"
  member   = "serviceAccount:${google_service_account.service["costing"].email}"
}

# Sales, Purchase and Customer all post to Accounting's internal ledger API, and
# Master seeds through the same seam. Granting the four rather than all seven
# keeps the list honest about who actually calls whom.
resource "google_cloud_run_v2_service_iam_member" "services_invoke_accounting" {
  for_each = toset(["sales", "purchase", "customer", "master"])

  name     = google_cloud_run_v2_service.api["accounting"].name
  location = var.region
  role     = "roles/run.invoker"
  member   = "serviceAccount:${google_service_account.service[each.value].email}"
}

# Sales asks Reporting for a credit check; Master seeds Inventory and Sales.
resource "google_cloud_run_v2_service_iam_member" "sales_invokes_reporting" {
  name     = google_cloud_run_v2_service.api["reporting"].name
  location = var.region
  role     = "roles/run.invoker"
  member   = "serviceAccount:${google_service_account.service["sales"].email}"
}

resource "google_cloud_run_v2_service_iam_member" "master_invokes_seeded" {
  for_each = toset(["inventory", "sales"])

  name     = google_cloud_run_v2_service.api[each.value].name
  location = var.region
  role     = "roles/run.invoker"
  member   = "serviceAccount:${google_service_account.service["master"].email}"
}

# ---------------------------------------------------------------------------
# Gateway
# ---------------------------------------------------------------------------
#
# The only public service. Its YARP cluster destinations are set here rather than
# in appsettings.Production.json because a Cloud Run URL is not knowable until
# the service exists — the committed file keeps its localhost defaults for a
# local run, and these environment variables override them, which is exactly what
# ASP.NET Core configuration precedence is for.
resource "google_cloud_run_v2_service" "gateway" {
  name     = "gateway"
  location = var.region

  ingress             = "INGRESS_TRAFFIC_ALL"
  deletion_protection = false

  template {
    service_account = google_service_account.service["gateway"].email

    scaling {
      # The public entry point, so it is the one service where a cold start is
      # the user's first impression. Still zero by default; this is the first
      # min_instance_count to raise.
      min_instance_count = var.min_instances
      max_instance_count = 10
    }

    containers {
      image = "${local.registry}/gateway:${var.image_tag}"

      ports {
        container_port = 8080
      }

      dynamic "env" {
        for_each = local.common_env
        content {
          name  = env.key
          value = env.value
        }
      }

      dynamic "env" {
        for_each = local.common_secret_env
        content {
          name = env.key
          value_source {
            secret_key_ref {
              secret  = google_secret_manager_secret.app[env.value].secret_id
              version = "latest"
            }
          }
        }
      }

      dynamic "env" {
        for_each = local.services
        content {
          name  = "ReverseProxy__Clusters__${env.key}__Destinations__d1__Address"
          value = google_cloud_run_v2_service.api[env.key].uri
        }
      }

      volume_mounts {
        name = "cloudsql"
        # Where Npgsql looks: the connection strings in Secret Manager are built
        # with Host=/cloudsql/<connection name>.
        mount_path = "/cloudsql"
      }

      resources {
        limits = {
          cpu    = "1"
          memory = "512Mi"
        }
      }
    }

    volumes {
      name = "cloudsql"
      cloud_sql_instance {
        instances = [google_sql_database_instance.main.connection_name]
      }
    }
  }

  depends_on = [google_project_service.required]
}

# Public. The services behind it are not, which is the point of the split.
resource "google_cloud_run_v2_service_iam_member" "gateway_public" {
  name     = google_cloud_run_v2_service.gateway.name
  location = var.region
  role     = "roles/run.invoker"
  member   = "allUsers"
}

# ---------------------------------------------------------------------------
# CostingEngine worker
# ---------------------------------------------------------------------------
#
# Not a request-driven service, and the settings say so.
#
# inv.StockMovements *is* the queue: the worker claims a movement with a guarded
# Pending -> InProgress update and costs it in order. A scale-to-zero service
# would stop claiming the moment traffic stopped, and movements would sit
# uncosted until the next request happened to wake something — which is the kind
# of failure that shows up as a month-end margin report being wrong.
resource "google_cloud_run_v2_service" "costing" {
  name     = "costing-worker"
  location = var.region

  ingress             = "INGRESS_TRAFFIC_INTERNAL_ONLY"
  deletion_protection = false

  template {
    service_account = google_service_account.service["costing"].email

    scaling {
      # Always one, never more. One is what keeps the loop running; more than one
      # is not needed because the guarded status claim means throughput is not
      # the constraint, and a second instance only adds contention on the same
      # rows.
      min_instance_count = 1
      max_instance_count = 1
    }

    containers {
      image = "${local.registry}/costing-worker:${var.image_tag}"

      dynamic "env" {
        for_each = local.common_env
        content {
          name  = env.key
          value = env.value
        }
      }

      dynamic "env" {
        for_each = local.common_secret_env
        content {
          name = env.key
          value_source {
            secret_key_ref {
              secret  = google_secret_manager_secret.app[env.value].secret_id
              version = "latest"
            }
          }
        }
      }

      volume_mounts {
        name = "cloudsql"
        # Where Npgsql looks: the connection strings in Secret Manager are built
        # with Host=/cloudsql/<connection name>.
        mount_path = "/cloudsql"
      }

      resources {
        limits = {
          cpu    = "1"
          memory = "512Mi"
        }
        # CPU outside request handling, which for a polling loop with no requests
        # at all is the only way it runs. Without this the container is throttled
        # to near zero between requests it never receives.
        cpu_idle = false
      }
    }

    volumes {
      name = "cloudsql"
      cloud_sql_instance {
        instances = [google_sql_database_instance.main.connection_name]
      }
    }
  }

  depends_on = [google_project_service.required]
}

# ---------------------------------------------------------------------------
# Migration job
# ---------------------------------------------------------------------------
#
# DatabaseMigrationService runs on Master's startup and creates databases and
# roles it finds missing. That is fine for one process and wrong for several: a
# deployment that cold-starts four Master instances at once has four of them
# racing to migrate the same schema.
#
# Running it as a job, once, before traffic shifts, is the fix — and the sharper
# form of the open question in CLAUDE.md about whether an auto-create-on-missing
# belongs in a production startup path at all.
resource "google_cloud_run_v2_job" "migrate" {
  name     = "migrate"
  location = var.region

  deletion_protection = false

  template {
    template {
      service_account = google_service_account.service["migrate"].email

      # A migration that fails should be looked at, not retried into a
      # half-applied schema.
      max_retries = 0
      timeout     = "900s"

      containers {
        image = "${local.registry}/master:${var.image_tag}"

        dynamic "env" {
          for_each = merge(local.common_env, {
            # Master's startup path migrates and then serves. The job wants the
            # first half only, and this is what the deploy workflow keys on.
            Bootstrap__OwnerEmail = var.bootstrap_owner_email
          })
          content {
            name  = env.key
            value = env.value
          }
        }

        dynamic "env" {
          for_each = local.common_secret_env
          content {
            name = env.key
            value_source {
              secret_key_ref {
                secret  = google_secret_manager_secret.app[env.value].secret_id
                version = "latest"
              }
            }
          }
        }

        volume_mounts {
          name       = "cloudsql"
          mount_path = "/cloudsql"
        }

        resources {
          limits = {
            cpu    = "1"
            memory = "1Gi"
          }
        }
      }

      volumes {
        name = "cloudsql"
        cloud_sql_instance {
          instances = [google_sql_database_instance.main.connection_name]
        }
      }
    }
  }

  depends_on = [google_project_service.required]
}
