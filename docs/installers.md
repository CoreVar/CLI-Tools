# Installation and signing

The core library, local ZIP packaging, static publication, and unsigned development installers need no account, hosted registry, or paid signing service. Signing is configured by each publisher. CoreVar does not sign other publishers' products.

## Portable archive signing

```console
cli-tools keygen --private-key-file release.private.pem --public-key-file release.public.pem
cli-tools sign --file dist/app.zip --private-key-file release.private.pem --key-id release-2026 --output dist/app.signature.json
cli-tools verify --file dist/app.zip --signature dist/app.signature.json --public-key-file release.public.pem
cli-tools publish static-cli --root site --public-base https://example.org/cli/ --tenant public --product app --version 1.0.0 --rid win-x64 --file dist/app.zip --signature dist/app.signature.json
```

The signature document contains only the digest, detached signature, key ID, and public key. Treat `release.private.pem` as a secret; never put it in the payload directory. Retain old public keys during key rotation so installed clients can verify a transition release.

For server publication, `publish cli` accepts the same `--signature` option. A complete release recipe supports `signatures`, a RID-to-signature-file map, and `requireSignatures: true`. Paths are relative to the recipe. All signatures are checked before uploads begin. Generated recipe installers embed the verified public keys and requested enforcement policy.

Registry environment settings:

```text
COREVAR_REGISTRY_SIGNING_KEYS_FILE=/run/config/publisher-keys.json
COREVAR_REGISTRY_SIGNED_CHANNELS=stable
```

The public-key file is a JSON object: `{ "tenant/product": { "release-2026": "PEM public key" } }`. Candidate channels may allow unsigned builds; promotion to a protected channel re-verifies stored artifact bytes against the owned keys.

For a CLI author:

```csharp
.SelfUpdate(new()
{
    Product = "app",
    CurrentVersion = "1.0.0",
    Catalog = new("https://example.org/cli/v1/public/products/app/catalog.json"),
    RequireSignature = true,
    TrustedPublicKeys = new Dictionary<string, string>
    {
        ["release-2026"] = publisherPublicKeyPem
    }
})
```

For bundled releases, supply `Readiness` to run setup/health checks in the candidate host. `ReleaseSetupCoordinator.UpdateAndVerifyAsync` also supports this flow. The candidate is installed in its immutable version directory while the prior host remains active. State is switched only after readiness succeeds. The callback must execute the candidate directly, rather than starting the active launcher. See the independent distribution example.

## Script installers and automation

Generate scripts with `cli-tools generate installers --product app --catalog <catalog-uri> --output dist`. Add `--trusted-keys keys.json --require-signature` for enforced signing. This public-key file maps key IDs directly to PEM strings.

```powershell
pwsh -NoProfile -File dist/install.ps1 -InstallDir "$env:LOCALAPPDATA\app" -NoPath -Quiet -Log install.log
```

```sh
sh dist/install.sh --install-dir "$HOME/.local/share/app" --no-path --quiet
```

Windows requires PowerShell 7.4+. Unix requires Python 3.9+, plus OpenSSL for signed artifacts. Both support version/channel selection, explicit trusted keys, required signatures, quiet operation, per-user installation, and no-PATH mode. Successful runs return 0; installation failures return 1. Argument parsing can return a platform-specific nonzero usage code. Quiet mode never requests input; errors remain visible.

Scripts verify archives and bundle digests before setup, refuse unsafe archive paths/links and conflicting version contents, use an installation lock, and activate state atomically. Re-running an identical version performs readiness again without overwriting its payload. Product setup failures restore prior module pointers and leave the prior host active. Historical versions remain available for rollback; cleanup is deliberately separate.

## Native Windows MSI and branded downloader

Install the free, pinned WiX 4 build tools on a Windows build machine:

```powershell
dotnet tool install wix --version 4.0.6 --tool-path .tools
.\.tools\wix.exe extension add WixToolset.Bal.wixext/4.0.6
cli-tools installer build --recipe windows-installer.json --wix .tools/wix.exe
```

Recipe:

```json
{
  "product": "Acme CLI",
  "publisher": "Acme",
  "version": "1.0.0",
  "sourceDirectory": "publish/win-x64",
  "executable": "acme.exe",
  "upgradeCode": "c5d7af2a-0bde-4dc2-a1f0-7233848776b8",
  "bundleUpgradeCode": "9ee2c0a9-58e2-4334-bfb6-5c590f044fc6",
  "architecture": "x64",
  "scope": "user",
  "outputDirectory": "dist/windows",
  "licenseUrl": "https://acme.example/license",
  "supportUrl": "https://acme.example/support"
}
```

Generate your own distinct upgrade GUIDs and keep them stable across versions for that product/scope/architecture. Supported architectures are x86, x64 and arm64. Choose `user` (no elevation) or `machine` when building; do not reuse upgrade IDs across scopes. MSI version limits apply. The root executable and all files under `sourceDirectory` are packaged, so use a clean publish directory. Publish self-contained if consumers should not need a .NET runtime.

`logoFile`, `iconFile`, and `themeFile` customize the native UI. All paths are relative to the recipe. Add `downloadUrl` pointing to the final MSI to build a small downloader; omitting it embeds the MSI for offline installation. Burn uses the same installation engine for its graphical and headless modes, and verifies the payload bound into the bundle at build time.

```powershell
& '.\Acme CLI-setup.exe' /quiet /norestart /log install.log
& '.\Acme CLI-setup.exe' /repair /quiet /norestart
& '.\Acme CLI-setup.exe' /uninstall /quiet /norestart
msiexec /i product.msi /qn /norestart /l*v install.log
```

Use `/passive` for progress without interaction. These are native Burn/MSI switches, not the earlier proposed `install --quiet` command syntax. Preserve native exit codes, including 3010 for restart required. Publish a new bundle with the same upgrade identities for upgrades. Native MSI installs are owned by Windows Installer; configure the application's self-update provider accordingly instead of pointing a direct updater at this directory.

For certificate signing, add:

```json
{
  "requireSigning": true,
  "signing": {
    "certificateThumbprint": "YOUR_CERTIFICATE_THUMBPRINT",
    "store": "My",
    "machineStore": false,
    "timestampUrl": "https://YOUR_TIMESTAMP_SERVICE",
    "signTool": "signtool"
  }
}
```

Install the publisher's code-signing certificate/provider in the build environment. The workflow signs and verifies the MSI, signs the detached Burn engine, reattaches it, and signs/verifies the final bundle. No key export or password argument is needed. This initial provider supports certificates exposed through the Windows certificate store; cloud-specific signing adapters are not required for development and are not bundled.

The build commands follow [WiX bundle signing](https://docs.firegiant.com/wix/tools/signing/) and [Microsoft SignTool](https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool). A detached RSA archive signature is a different mechanism from Windows Authenticode publisher identity.

## Package-manager metadata

WinGet, Homebrew and RPM generation take `--license` explicitly. WinGet takes an executable path and architecture; RPM takes its native architecture name. MSIX App Installer generation requires the actual certificate publisher distinguished name with `--publisher`. Generator output does not impose the framework's MIT license on a consuming product. Validate metadata with the destination package manager before submission.
