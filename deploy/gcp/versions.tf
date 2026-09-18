terraform {
  required_version = ">= 1.6"

  required_providers {
    google = {
      source = "hashicorp/google"
      # v6 is where google_cloud_run_v2_service carries the settings this
      # configuration relies on — scaling on the worker and the Cloud SQL volume
      # mount among them.
      version = "~> 6.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
}

provider "google" {
  project = var.project_id
  region  = var.region
}
