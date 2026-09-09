# Contributing

CLI Tools is an MIT-licensed community library. Contributions do not require a CoreVar account, a cloud subscription, or signing credentials.

## Build and test

Install the .NET 10 SDK and .NET 8 runtime. Python 3.9+ runs the package/distribution acceptance tests. Windows bootstrap tests require PowerShell 7.4+; signed Unix bootstrap tests require OpenSSL.

From the repository root:

```console
dotnet restore src/CLI-Tools.sln
dotnet build src/CLI-Tools.sln -c Release --no-restore
dotnet test src/CLI-Tools.sln -c Release --no-build
python tests/package_consumer.py
python "examples/05 - DistributionRecipe/acceptance.py"
```

The acceptance scripts create isolated temporary directories, print their locations, and retain command logs. The distribution fixture binds a registry to loopback, uses temporary credentials, and leaves the user's PATH unchanged. Its signing keys are test fixtures, not release identities.

To build local NuGet packages:

```console
dotnet pack src/CommandLineInterface/CommandLineInterface.csproj -c Release -o .artifacts/packages -p:PackageVersion=99.0.0-local.1
dotnet pack templates/ProjectTemplates.csproj -c Release -o .artifacts/packages -p:PackageVersion=99.0.0-local.1
```

Pack the abstractions package at the same version when consuming the core package locally. `tests/package_consumer.py` performs the complete isolated sequence. Template packaging stamps the generated projects with the package version; it does not modify source templates.

## Choosing where to contribute

| Project | Responsibility |
| --- | --- |
| Abstractions / CommandLineInterface / SourceGenerator | Command authoring, binding, help, execution, generated components |
| Modules | Out-of-process modules and private runtime environments |
| Distribution / Launcher | Trust verification, installation state and self-update |
| Publishing / CoreVar.CliTools | Packaging, static/server publication, installer generation |
| Registry | Optional self-hosted distribution service |
| Blazor | Optional browser console |

Keep the basic CLI usable without the optional modules, registry, or publishing packages. CoreVar branding belongs in publisher recipes and examples. Do not introduce mandatory accounts, telemetry, paid services, or signing credentials into local development.

## Pull requests

Explain the user-visible problem, the resulting behavior, and how you verified it. Add regression coverage for failures and compatibility boundaries. For a breaking API or protocol change, open an issue first so maintainers and consumers can discuss migration.

Contributor CI runs unit/registry tests, fresh-package consumers, signed distribution acceptance, and Native AOT on Windows, Linux, and macOS. Windows CI also exercises quiet installer install, repair, upgrade, and uninstall. Release publication depends on these checks. Fork PRs can run validation without publisher secrets.

## Troubleshooting

- `NETSDK1045`: building the repository requires .NET 10; targeting .NET 8 applications is still supported.
- Missing framework while running tests: install both .NET 8 and .NET 10 runtimes.
- `pwsh` not found: install PowerShell 7.4+ and add it to PATH; Windows PowerShell 5.1 is not supported by the bootstrap script.
- WiX not found: follow [Windows installer setup](docs/installers.md). It is optional for ordinary development.
- Installer operation already running: let the active process finish. Lock files intentionally remain on disk; their existence alone does not mean a process holds the lock.
- Signature rejected: compare the configured key ID and independently obtained public key. Do not bypass required-signature policy to fix a publishing error.

Report vulnerabilities through the process in [SECURITY.md](SECURITY.md).
