variable "project_id" {
  description = "The Google Cloud project everything is created in."
  type        = string
}

variable "region" {
  description = <<-EOT
    The region for Cloud Run, Cloud SQL and the bucket.

    asia-south1 is Mumbai, and the default for a reason rather than by habit:
    this is an Indian GST product, the data is Indian businesses' books, and the
    latency that matters is to Indian users. asia-south2 (Delhi) is the
    alternative. Changing this after the first apply replaces the Cloud SQL
    instance, so it is a decision to make once.
  EOT
  type        = string
  default     = "asia-south1"
}

variable "image_tag" {
  description = <<-EOT
    The tag every service image is deployed at, normally the commit SHA.

    Not "latest". A mutable tag makes two revisions of the same service
    potentially different builds, and makes a rollback a matter of hoping the
    old image is still what that tag pointed at.
  EOT
  type        = string
}

variable "sql_tier" {
  description = <<-EOT
    The Cloud SQL machine type. db-custom-2-7680 is two vCPUs and 7.5 GB, which
    is the smallest shape that is not a shared-core burstable one.

    Shared-core (db-f1-micro, db-g1-small) is a false economy here: this database
    holds every tenant's books, and the accounting suite's deferred triggers and
    guarded updates are latency-sensitive under concurrency.
  EOT
  type        = string
  default     = "db-custom-2-7680"
}

variable "sql_deletion_protection" {
  description = <<-EOT
    Whether Terraform may destroy the database instance.

    On by default, and worth leaving on. `terraform destroy` against a
    misconfigured workspace is otherwise the last thing that happens to a
    customer's books.
  EOT
  type        = bool
  default     = true
}

variable "min_instances" {
  description = <<-EOT
    Minimum instances for the HTTP services. Zero is the default and is right
    for a product with no released deployment: nothing is running, nothing is
    billed, and the first request of the day pays a cold start.

    Raise it to 1 when cold starts start showing up in the UI. The costing worker
    ignores this — see its own setting in cloud_run.tf.
  EOT
  type        = number
  default     = 0
}

variable "bootstrap_owner_email" {
  description = <<-EOT
    The address the first account is created for, read by Master's
    Bootstrap:OwnerEmail. The account is created with no password; the only way
    in is the ordinary reset flow, which proves control of the mailbox.

    Empty creates nothing, which is the right default for an environment that
    already has its users.
  EOT
  type        = string
  default     = ""
}
