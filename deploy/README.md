# Self-hosting the CoreVar CLI Registry

CoreVar operates no required service. These templates deploy the same open-source container into infrastructure owned and billed by the CLI author.

- `docker/compose.yml`: one-command local, server, NAS, or VM deployment.
- `kubernetes/registry.yaml`: Kubernetes with a persistent volume and tenant key secret.
- `azure/main.bicep`: Azure Container Instances with an Azure Files volume.
- `aws/cloudformation.yml`: a small EC2 Docker host with persistent root storage.
- `gcp/main.tf`: a small Compute Engine Docker host with persistent root storage.

The VM templates intentionally use ordinary Docker so they have no proprietary runtime dependency. Put TLS in front of the registry before using it across an untrusted network. Change every example key before deployment.

For a service-free option, run `cli-tools publish static-cli ...` against a GitHub Pages checkout, GitHub Release assets, or any static web host. Static catalogs support public downloads; private authenticated publishing happens through the host's normal Git workflow.
