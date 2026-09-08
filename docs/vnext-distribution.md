# CoreVar CLI distribution and modules

This document defines the architecture for the release following v10. CoreVar does not operate a required hosted service. Every component is open source and can use a self-hosted registry, a static HTTP/Git-backed catalog, or a native package manager.

## Principles

- Native package managers own installations they created. The updater delegates to `dotnet tool`, winget, Homebrew, APT, or RPM when applicable.
- Direct installations use a stable launcher, versioned directories, atomic activation, health checks, and rollback.
- Runtime-installed modules execute out of process. This keeps the host Native-AOT compatible, avoids dependency conflicts, and makes modules language neutral.
- Artifacts are immutable, content addressed, checksummed, and signed. Channels are mutable pointers to immutable releases.
- Publishing, installation, and operation never require a CoreVar-operated service.

## Module kinds

Built-in modules are compiled with the host and use the normal component API: `cli.Module<CloudModule>()`.

Installable modules contain a `corevar.module.json` manifest and one or more platform artifacts. The host projects the cached command description into its help and completion trees and launches the selected artifact as a child process.

Python modules receive an isolated `.venv`. They can use a declared system Python version or ship a portable interpreter. Node modules receive an isolated application directory and `node_modules` restored with `npm ci`; they can use a declared system Node version or ship a portable runtime. Runtime directories are private to a module version and are replaced transactionally.

## Local layout

```text
<root>/
  bin/                         stable launcher
  versions/<version>/          immutable CLI versions
  modules/<id>/versions/<v>/   immutable module versions
  modules/<id>/current.json    active module pointer
  cache/                       content-addressed downloads
  state.json                   installation provider and channel
```

## Process contract

Protocol `corevar.module.process/1` reserves these operations:

- `--corevar-describe`: writes the module manifest as JSON.

Each invocation receives its arguments unchanged and runs in an isolated child-process environment. Hosts can supply short-lived credentials through `IModuleCredentialProvider`; credentials are added only to that child process and are never appended to command arguments or written to process-global environment variables. `ModuleInvocationContext` routes stdin, stdout, stderr, cancellation, services, and output limits through the host's `IConsoleControl`, so the same module works in native terminals, Blazor terminals, and multi-user portals without exposing an arbitrary shell.

A command can declare an `output` object (`mediaType`, optional `schema`, and the argument used to request it). This lets portals and automation discover structured results without hard-coding module behavior. Command and option descriptions are also emitted in PowerShell completion tooltips.

### Reproducible module bundles

`corevar.cli.bundle/1` pins an immutable module snapshot. A bundle has separate `hostCompatibility` and `frameworkCompatibility` ranges, a default module `catalog`, and members pinned by ID, version, and SHA-256. A member may override its catalog and restrict runtime identifiers, platforms, or architectures. Catalogs may use the registry's existing public module-catalog/download routes; publishing remains protected by the registry's existing authentication policies.

```json
{
  "schema": "corevar.cli.bundle/1",
  "id": "vendor-suite",
  "snapshot": "2026.09.08.1",
  "hostCompatibility": "[0.1.0,0.2.0)",
  "frameworkCompatibility": "[10.1.0,11.0.0)",
  "catalog": "https://registry.example/v1/tenant/modules/catalog.json",
  "modules": [
    { "id": "vendor.product", "version": "10.1.0", "sha256": "...", "required": true,
      "platforms": ["win", "linux", "osx"], "architectures": ["x64", "arm64"] }
  ]
}
```

Bundle manifests can be loaded from a local path or HTTPS URI and are verified against the SHA-256 stored in product release metadata. Required incompatibility or installation failure makes `ModuleBundleInstallResult.IsComplete` false and rolls back changes made earlier in the operation. Optional members report explicit skipped/incompatible/failure results.

Hosts can install before projecting external commands:

```csharp
builder.UseModuleBundleBootstrap(options => options
    .Manifest(Environment.GetEnvironmentVariable("COREVAR_MODULE_BUNDLE")!,
        Environment.GetEnvironmentVariable("COREVAR_MODULE_BUNDLE_SHA256")!)
    .HostVersion("0.1.0")
    .Snapshot(Environment.GetEnvironmentVariable("COREVAR_MODULE_BUNDLE_SNAPSHOT")!)
    .FrameworkVersion("10.1.0")
    .Root(Path.Combine(userHome, ".corevar", "distribution")))
  .ExternalModules(Path.Combine(userHome, ".corevar", "distribution"));
```

Product release manifests may include `bundle: { manifest, sha256, snapshot, root }` and `postInstallArguments: ["setup"]`. Generated PowerShell and POSIX installers download and verify that manifest, expose it to the installed host, invoke the configured arguments with the installed stable launcher, and print success only after bootstrap exits successfully. Initial bundle integrity is SHA-256 only; no signature verification is claimed.
- Normal invocation receives command arguments unchanged and inherits the terminal streams.
- `COREVAR_MODULE_PROTOCOL`, `COREVAR_MODULE_ID`, `COREVAR_MODULE_VERSION`, and `COREVAR_CLI_VERSION` describe the host.
- Exit codes and Ctrl+C flow through unchanged.

The cached manifest is authoritative for offline help and completion. A future streaming JSON-RPC transport can add structured prompts without changing package or installation formats.

## Registry modes

The same catalog API is supported by static JSON plus blobs; the containerized registry with filesystem, object-storage, or OCI backing; and native package sources generated from the canonical release. The container includes no dependency on CoreVar infrastructure. Cloud templates deploy it into the user's own subscription/account/project.

## Delivery order

1. Contracts and built-in modules.
2. External process protocol and isolated language runtimes.
3. Module install/update/remove/rollback.
4. Stable launcher and CLI self-update.
5. Signed release manifests and channels.
6. Containerized registry.
7. Publishing CLI and CI integrations.
8. Bootstrap and native-package generators.
9. Rollouts, revocation, documentation, and release verification.
