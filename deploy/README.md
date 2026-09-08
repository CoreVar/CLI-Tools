# Self-hosting the CoreVar CLI Registry

CoreVar operates no required service. These templates deploy the same open-source container into infrastructure owned and billed by the CLI author.

- `docker/compose.yml`: one-command local, server, NAS, or VM deployment.
- `kubernetes/registry.yaml`: Kubernetes with a persistent volume and tenant key secret.
- `azure/main.bicep`: Azure Container Instances with an Azure Files volume.
- `aws/cloudformation.yml`: a small EC2 Docker host with persistent root storage.
- `gcp/main.tf`: a small Compute Engine Docker host with persistent root storage.

The VM templates intentionally use ordinary Docker so they have no proprietary runtime dependency. Put TLS in front of the registry before using it across an untrusted network. Change every example key before deployment.

## Reverse proxies and URL prefixes

The registry can be exposed at a domain root or below a prefix such as
`https://downloads.example.com/cli-registry`. Both the generated installer URLs and
catalog artifact URLs honor these settings:

- `COREVAR_REGISTRY_PATH_BASE=/cli-registry` makes ASP.NET Core serve every route
  below that prefix. Use this when the proxy preserves the prefix upstream.
- `COREVAR_REGISTRY_PUBLIC_BASE_URL=https://downloads.example.com/cli-registry/`
  explicitly controls URLs written into catalogs and installers. Use this when an
  application gateway strips or rewrites the external prefix.
- `COREVAR_REGISTRY_TRUST_FORWARDED_HEADERS=true` honors `X-Forwarded-For`,
  `X-Forwarded-Host`, and `X-Forwarded-Proto`. Enable it only when the registry is
  reachable exclusively through trusted proxy infrastructure. An explicit public
  base URL is preferred when that cannot be guaranteed.

For example, publish through the same externally visible base URL:

```sh
cli-tools publish cli \
  --endpoint https://downloads.example.com/cli-registry/ \
  --tenant public --product acme --version 1.0.0 \
  --rid linux-x64 --file ./acme-linux-x64.zip
```

For a service-free option, run `cli-tools publish static-cli ...` against a GitHub Pages checkout, GitHub Release assets, or any static web host. Static catalogs support public downloads; private authenticated publishing happens through the host's normal Git workflow.
