# Local prompt package qualification

This is evidence for unpublished development packages, not a feed release. See `qualification.json` for exact source and package hashes, framework tests, and separately identified CoreMQ product evidence. The API guide is [interactive-prompts.md](../../interactive-prompts.md).

The included standalone consumer has no project references. Copy this directory to a temporary workspace and place the matching four packages in a `packages` subdirectory. Set `NUGET_PACKAGES` and `NUGET_HTTP_CACHE_PATH` to new empty directories, then run:

```text
dotnet restore --configfile NuGet.Config --locked-mode --no-http-cache
dotnet build -c Release --no-restore
dotnet bin/Release/net8.0/Consumer.dll
dotnet bin/Release/net10.0/Consumer.dll
```

Run the consumer with redirected input/output, as in CI; it asserts that the native reader refuses interactive capability there. The injected prompt returns a synthetic test value and verifies generated parameter binding. Both executions print `prompt-consumer-passed`. The source mapping keeps CoreVar packages exclusively local and allows public Microsoft dependencies from NuGet.org.

To build your own package artifacts, check out the source commit in the manifest and `dotnet pack` Abstractions, Core, Blazor and Modules with the same `PackageVersion` property. Local rebuild bytes may differ with SDK/platform; retain the manifest hashes when qualifying the supplied artifacts. Do not overwrite or publish an existing version with different bytes.
