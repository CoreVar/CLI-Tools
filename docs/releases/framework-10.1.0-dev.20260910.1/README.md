# Framework private-feed release evidence

Abstractions, Core and Blazor `10.1.0-dev.20260910.1` were published to CoreVar Toolkit from commit `e63e31ac95e5236b275221db3e499165c49c7a42`. See `release-manifest.json` for source and package SHA256 hashes. Publication used immutable pushes without skipping duplicates.

The included consumer passed restore, a second locked restore with separate empty package and HTTP caches, Release build and execution for .NET 8 and .NET 10. Both terminal JavaScript assets were present in each static web asset manifest. Downloaded package hashes matched the prepared release artifacts.

`NuGet.Config` maps CoreVar packages exclusively to Toolkit and other dependencies to NuGet.org. Toolkit-only restore failed because `Microsoft.AspNetCore.App.Internal.Assets`, requested by the .NET 10 Web SDK, was unavailable there. Standard authenticated NuGet access to Toolkit is required; no credentials are included.

To reproduce, copy this directory outside the repository, set `NUGET_PACKAGES` and `NUGET_HTTP_CACHE_PATH` to new empty directories, and run:

```text
dotnet restore --configfile NuGet.Config --locked-mode --no-http-cache
dotnet build -c Release --no-restore
dotnet bin/Release/net8.0/Consumer.dll
dotnet bin/Release/net10.0/Consumer.dll
```

The root Blazor reference is exact. Internal package dependencies use SDK-generated minimum ranges; preserve the lockfile and locked restore to retain the verified closure.

This release does not publish to public NuGet.org or deploy either portal. The shared terminal auto-scroll implementation is included in Blazor; portal deployment remains a separate step.
