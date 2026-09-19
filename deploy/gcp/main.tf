terraform {
  required_providers { google = { source = "hashicorp/google", version = "~> 6.0" } }
}
variable "project" { type = string }
variable "region" { type = string, default = "us-central1" }
variable "tenant" { type = string, default = "demo" }
variable "publish_key" { type = string, sensitive = true }
variable "image" { type = string, default = "ghcr.io/corevar/cli-registry:latest" }
provider "google" { project = var.project, region = var.region }
resource "google_compute_firewall" "registry" {
  name = "corevar-cli-registry"
  network = "default"
  allow { protocol = "tcp", ports = ["8080"] }
  source_ranges = ["0.0.0.0/0"]
  target_tags = ["corevar-cli-registry"]
}
resource "google_compute_instance" "registry" {
  name = "corevar-cli-registry"
  zone = "${var.region}-a"
  machine_type = "e2-micro"
  tags = ["corevar-cli-registry"]
  boot_disk { initialize_params { image = "debian-cloud/debian-12", size = 10 } }
  network_interface { network = "default", access_config {} }
  metadata_startup_script = <<-SCRIPT
    #!/bin/sh
    apt-get update && apt-get install -y docker.io
    systemctl enable --now docker
    mkdir -p /opt/corevar-cli-registry
    docker run -d --restart unless-stopped --name corevar-cli-registry -p 8080:8080 \
      -e COREVAR_REGISTRY_DATA=/data -e 'COREVAR_REGISTRY_API_KEYS=${var.tenant}=${var.publish_key}' \
      -v /opt/corevar-cli-registry:/data ${var.image}
  SCRIPT
}
output "endpoint" { value = "http://${google_compute_instance.registry.network_interface[0].access_config[0].nat_ip}:8080/" }
