# Independent modular CLI distribution example

This runnable example builds `sample-cli` and a separate native `sample-module`.
It uses public CLI Tools commands and APIs, with no company identity server,
marketplace product, cloud account or private repository dependency.

From the repository root:

```powershell
python "examples/05 - DistributionRecipe/acceptance.py"
```

Prerequisites: .NET 10 SDK, Python 3, and PowerShell 7.4+ (`pwsh`) on Windows.
On Unix the generated installer requires Python 3.9+ and OpenSSL for RSA signature verification.
The example's apphosts are framework-dependent and use the installed .NET runtime.
For a runtime-independent production distribution, publish the host, launcher and
native module self-contained for each supported RID before packaging them.

The runner creates a fresh temporary directory and binds a registry to loopback.
It generates ephemeral fixture RSA keys and a publishing credential, supplies the credential through the
environment, and stops the registry on exit. It keeps artifacts, `commands.log`,
`registry.log` and a successful `result.json` in the printed directory for inspection.
Installation uses an isolated root and does not change the user's Windows PATH;
Unix launcher links are created under an isolated HOME.

The test executes these public command surfaces as real child processes:

```text
cli-tools package --source <module-publish-dir> --output <module.zip>
cli-tools publish module --endpoint <registry> --tenant sample --id sample-module --version <version> --file <module.zip> --description "Independent sample module"
cli-tools package --source <host-publish-dir> --output <host.zip> --launcher <published-launcher>
cli-tools publish release --recipe <recipe.json>
```

The recipe uses paths relative to its own directory. Publication creates the
complete release and emits `installers/install.ps1` and `installers/install.sh`.
The generated installer downloads the host and required module bundle and invokes
the host's setup contract before reporting success.

`SampleHost` shows how to register external modules, install a pinned bundle with
`ModuleBundleInstaller`, and use `ReleaseSetupCoordinator` for update readiness and
host rollback. Its readiness callback runs setup in the newly installed host.
`SampleModule` reports its process ID and module protocol/version, proving actual
native child dispatch rather than only command-tree discovery.

Acceptance covers signed publication and signature verification, repeated quiet installation, initial install/dispatch, a successful host update, failed bootstrap upgrade preserving the launcher and active host, an unavailable
required module download after valid publication, restoration of the previous host
and usable module, successful retry, same-version readiness, and rejection of a
required bundle member's incorrect digest before stable-channel promotion.
Fault injection modifies only the temporary registry's own sample artifact.

This is a bootstrap-installer example. It does not compile MSI/PKG binaries or
provide Authenticode signing/notarization. Portable RSA signing is exercised. Execution on one OS does not qualify other OS/RID
combinations; run this fixture on each supported native runner.

CI runs this fixture on native Windows, Linux and macOS runners. A passing result records the tested RID and cases in `result.json`; do not infer qualification of other platforms from one run.
