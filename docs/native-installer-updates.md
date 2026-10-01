# Native installer updates

`NativeInstallerUpdateClient` provides a reusable native Windows update path for
applications owned by MSI/Burn. Keep direct ZIP and package-manager installations
on their existing provider paths. Native updates launch the publisher's signed
setup UI without silent arguments; they never unpack application bytes into an
MSI-owned installation directory.

Compile `NativeInstallerUpdatePolicy` into the signed application, including the
reviewed manifest origin, actual publisher certificate subject, product ID,
architecture (`win-x64` or `win-arm64`), signed setup product name and company name.
Do not accept the publisher identity or manifest origin from downloaded metadata
or a mutable local JSON override. Configure trust only after the publisher's
signing profile has been verified.

The strict manifest is compatible with CoreDM Station's schema 1:

```json
{
  "schemaVersion": 1,
  "productId": "example",
  "architecture": "win-x64",
  "version": "1.0.1",
  "installer": {
    "url": "https://publisher.example/native/1.0.1/setup.exe",
    "sha256": "64_HEXADECIMAL_CHARACTERS",
    "sizeBytes": 123456
  }
}
```

`CheckAsync` accepts only a newer three-part MSI version, a direct HTTP 200,
at most 16 KiB of manifest data and the exact configured product/architecture.
Unknown JSON properties are rejected. The download must remain on the manifest's
HTTPS origin, with no credentials, query strings or fragments. The default HTTP
handler disables redirects. Installer responses are bounded at 768 MiB and must
match both declared size and SHA256. Each download uses a distinct cache directory;
failed downloads remove only their own directory.

Use `WindowsNativeInstallerVerifier` in production. It invokes Windows trust with
revocation checks, requires a verified timestamp, pins the actual certificate
subject using ordinal equality, and verifies signed product/company/version
metadata. Unit tests with an injected verifier prove protocol behavior only;
they do not establish a publicly trusted signature.

`LaunchVerified` rechecks cached bytes and Windows trust while holding the file
read-only during process creation. It supplies no command-line switches. The
consuming application must release active operations and exit before MSI replaces
its own executable. A detached handoff must perform the same verification in the
new process. Never run a direct ZIP updater over MSI-owned files. Retain normal
Windows setup upgrade/uninstall and rollback behavior.

Real release qualification requires the publisher's verified signing profile,
exact signed setup, correct installer module bootstrap, configured feed, clean
Windows installation/upgrade checks and a guarded TEST/PROD promotion route.

## Native registry publication

The distribution service serves native releases through separate product-scoped
routes, leaving ZIP/module catalogs and storage unchanged:

- PUT and GET `/v1/{tenant}/native/products/{product}/installers/{version}/{rid}/setup.exe`
- GET `/v1/{tenant}/native/products/{product}/channels/{channel}/{rid}/manifest.json`
- POST `/v1/{tenant}/native/products/{product}/channels/{channel}/{rid}/{version}`
- POST `/v1/{tenant}/native/products/{product}/revocations/{version}/{rid}`

Mutation requests retain existing product authorization: `release.publish`,
`release.promote` and `release.revoke`. Native mutations are disabled when anonymous
publishing is configured. Read authentication follows the registry's existing
read policy. No public-storage setting is needed; the API serves only known native
product artifacts, and revoked feeds/artifacts are hidden.

Enroll a publisher's public proof key through the existing
`COREVAR_REGISTRY_SIGNING_KEYS_FILE` configuration, or the mutually exclusive
`COREVAR_REGISTRY_SIGNING_KEYS_JSON` environment value for managed deployments.
Both accept public-only RSA keys of at least 2048 bits. Preserve all existing
product entries when updating enrollment. Its shape is
`{"tenant/product":{"key-id":"PUBLIC_KEY_PEM"}}`. The service never trusts a key
supplied by an upload. Store the private proof key in the publisher's authorized
CI secret store; this is separate from the cloud code-signing identity.

```powershell
cli-tools sign --file signed-setup.exe --private-key-file publisher-private.pem --key-id publisher --output proof.json
cli-tools publish native --endpoint https://registry.example/ --tenant publisher --product example --version 1.0.1 --rid win-x64 --file signed-setup.exe --policy reviewed-producer-policy.json --signature proof.json
cli-tools publish native-promote --endpoint https://registry.example/ --tenant publisher --product example --version 1.0.1 --rid win-x64 --channel dev
```

For CI secret-store handoffs, use `sign --private-key-env VARIABLE_NAME` instead
of `--private-key-file`. The private PEM remains in process memory/environment;
the CLI never writes it to disk or prints it. Set the value only around the child
invocation and restore/remove it in the owning pipeline's `finally` block. Select
exactly one key source. The argument is the variable name, never the private key.

Supply registry authentication through `COREVAR_REGISTRY_TOKEN`. The producer policy
uses the same fields as `NativeInstallerUpdatePolicy`, with the actual reviewed
publisher/product/company. A mutable producer recipe is a build input; consuming
applications must still compile their own immutable trust policy. The Windows
publishing client verifies the real timestamped Authenticode identity before any
upload. The service additionally requires an enrolled RSA-PSS/SHA256 proof over
the uppercase hexadecimal artifact digest and rejects PE files without an embedded
certificate table. The Linux service does not establish Windows Authenticode
chain trust; both the Windows producer and native update client must perform that
verification. Synthetic table-presence fixtures test storage only and are never
release artifacts.

Publication is immutable and does not change a channel. Promotion separately
rechecks publisher proof and the payload; keep human TEST/PROD gates in the owning
release pipeline. Revocation prevents further promotion and hides the feed and
artifact without deleting retained release bytes. Removing an enrolled proof key
also hides its native artifacts. Independent module updates retain their catalogs.
