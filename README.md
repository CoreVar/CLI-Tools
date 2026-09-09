# CLI Tools for .NET

Build command-line applications with fluent commands or source-generated components. CLI Tools is free, open source, and MIT licensed. No CoreVar account or hosted service is required.

> This branch contains the next prerelease. The quickstart targets that API; use matching prerelease packages or [build local packages](CONTRIBUTING.md).

## Your first command

```console
dotnet new console -n MyCli --framework net8.0
cd MyCli
dotnet add package CoreVar.CommandLineInterface --prerelease
```

Replace `Program.cs`:

```csharp
using CoreVar.CommandLineInterface;

await CliApp.RunAsync(cli => cli
    .Command("hello", command => command
        .OnExecute(() => Console.WriteLine("Hello!"))));
```

```console
dotnet run -- hello
```

Or start from a template:

```console
dotnet new install CoreVar.CommandLineInterface.Templates --prerelease
dotnet new cli-simple -n MyCli
```

Use `cli-components` for source-generated command classes. Packaged templates reference the matching framework package version. Both start as ordinary one-shot CLIs; opt into `.EnableRepl()` when you want an interactive shell.

## What to use

| Need | Package or project |
| --- | --- |
| Commands, options, help, validation, middleware, cancellation, testing | `CoreVar.CommandLineInterface` |
| Out-of-process modules in .NET, Python, Node or another language | `CoreVar.CommandLineInterface.Modules` |
| Signed archive verification, self-update and rollback | `CoreVar.CommandLineInterface.Distribution` |
| Packaging and static/server publication | `CoreVar.CommandLineInterface.Publishing` / `CoreVar.CliTools` tool |
| Optional self-hosted registry | `CommandLineInterface.Registry` |
| Optional browser console | `CoreVar.CommandLineInterface.Blazor` |

The basic CLI has no dependency on the optional registry, modules, or publishing stack. The core runtime and source-generated components target .NET 8 and .NET 10 and support trimming/Native AOT. Blazor has its own platform publishing constraints.

## Release status

The modules/distribution/installer work on this branch is **prerelease**. The examples below describe this checkout and packages built from it; they may not exist in the latest stable NuGet package. Use the corresponding prerelease packages together, or follow [the contributor guide](CONTRIBUTING.md) to build local packages.

Unit tests and executable acceptance fixtures are the source of qualification. CI covers Windows, Linux and macOS; a locally passing Windows run alone does not qualify another platform. Native Windows installers support MSI/Burn build and a certificate-store signing provider. Signing a release with a publicly trusted certificate requires the publisher's credentials; ordinary development and unsigned builds do not.

## Optional modules and distribution

```csharp
await CliApp.RunAsync(cli => cli
    .Module<CloudModule>()
    .ExternalModules()
    .ModuleManagement()
    .SelfUpdate(new()
    {
        Product = "acme",
        CurrentVersion = "1.0.0",
        Catalog = new("https://cli.acme.example/v1/public/products/acme/catalog.json")
    }));
```

External modules run out of process. Python and Node modules can use private dependency environments. These isolate dependencies, not untrusted code; installed modules have the user's privileges.

Start with static files if you do not need a registry service:

```console
cli-tools package --source ./publish --launcher ./launcher/corevar-cli-launcher --output ./dist/acme-linux-x64.zip
cli-tools publish static-cli --root ./site --public-base https://example.org/acme/ --tenant public --product acme --version 1.0.0 --rid linux-x64 --file ./dist/acme-linux-x64.zip
cli-tools generate installers --product acme --catalog https://example.org/acme/v1/public/products/acme/catalog.json --output ./dist/installers
```

Use `.exe` launcher paths for Windows artifacts. Serve the generated site from any trusted static HTTPS host. Static artifacts are immutable: publish a new version when bytes change. For signed distribution, configure independently trusted public keys and required-signature policy; see [installation and signing](docs/installers.md).

The optional registry can run in your own environment with `docker compose -f deploy/docker/compose.yml up -d`. See [self-hosting](deploy/README.md) for access configuration and deployment templates. It is not needed to author a CLI, package files, or publish a static catalog.

## Guides and examples

- [Command authoring guide](docs/v10.md): binding, middleware, help/completion, testing, and generated documentation.
- [Installation and signing](docs/installers.md): quiet installers, publisher branding, native MSI/Burn, signing keys, and release policy.
- [Independent distribution example](examples/05%20-%20DistributionRecipe/README.md): signed publication, install, native module dispatch, update, and failure recovery without a company account.
- [Distribution protocol](docs/vnext-distribution.md): architecture and protocol details; consult executable tests and the installer guide for implemented behavior.
- [Example publication workflow](examples/workflows/publish-static-registry.example.yml): copy and adapt to your own project.

## Contributing

Install the .NET 10 SDK and .NET 8 runtime, then run:

```console
dotnet test src/CLI-Tools.sln -c Release
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for clean-package tests, platform acceptance tests, repository layout, and troubleshooting. Contributions may improve code, documentation, accessibility, tests, or examples. [SECURITY.md](SECURITY.md) describes vulnerability reporting and trust boundaries.

## License

[MIT](LICENSE). Publisher identity, branding, and the license of a product built with CLI Tools remain configurable by its author.
