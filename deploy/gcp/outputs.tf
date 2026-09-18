output "gateway_url" {
  description = "The public entry point. Every API is reached through this."
  value       = google_cloud_run_v2_service.gateway.uri
}

output "api_urls" {
  description = "Internal Cloud Run URLs, one per service. Not publicly reachable."
  value       = { for k, v in google_cloud_run_v2_service.api : k => v.uri }
}

output "sql_connection_name" {
  description = "project:region:instance, as the Cloud SQL socket path and the auth proxy both want it."
  value       = google_sql_database_instance.main.connection_name
}

output "documents_bucket" {
  description = "The uploads and archive bucket."
  value       = google_storage_bucket.documents.name
}

output "registry" {
  description = "Artifact Registry path images are pushed to."
  value       = local.registry
}

output "migrate_job" {
  description = "The Cloud Run job that applies migrations. The deploy workflow executes this before shifting traffic."
  value       = google_cloud_run_v2_job.migrate.name
}

# The generated SQL password, so an operator can reach the database with psql
# without resetting it.
#
# Marked sensitive, which keeps it out of plan and apply output — but it is in
# the state file in clear, as every generated credential in Terraform is. That is
# the reason the backend for this workspace must be a GCS bucket with versioning
# and restricted IAM rather than a file on somebody's laptop.
output "sql_password" {
  description = "Password for the billbook database user."
  value       = random_password.sql.result
  sensitive   = true
}
