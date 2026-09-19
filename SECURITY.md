# Security reporting and trust boundaries

Please use GitHub's **Security → Report a vulnerability** private reporting flow when available. If the repository does not offer it, open an issue titled **Private security contact requested**, without exploit details, credentials, or sensitive logs, so maintainers can arrange a private channel.

Include the affected version, operating system, a minimal reproduction, impact, and any proposed fix. Avoid publishing working exploit details before maintainers have had an opportunity to respond. This volunteer-maintained project does not promise a fixed response SLA.

## Distribution trust

- A SHA-256 digest detects corruption against the catalog. An RSA-PSS signature additionally verifies the artifact against a publisher key configured independently by the consumer.
- Set `RequireSignature` and supply `TrustedPublicKeys` for signed self-update. An optional signature is still verified when present; unknown keys fail closed.
- Signature files contain a public key for publisher-side checks. That key is not automatically enrolled by consumers or the registry.
- Registry signing keys are scoped by `tenant/product`. `COREVAR_REGISTRY_SIGNED_CHANNELS` enforces signatures on direct publication and promotion into those channels.
- The current detached signature covers the artifact digest. It does not authenticate catalog metadata such as version labels, revocations, bundle URLs, or setup arguments. Serve catalogs and generated scripts over trusted HTTPS or trusted local storage; protect the publishing endpoint and static-hosting account. Signed metadata and replay protection are not implemented.
- Modules execute code with the user's privileges. Out-of-process execution and private Python/Node environments are dependency isolation, not a security sandbox.
- Installer rollback preserves the host state and module pointers on handled setup failures. A product's setup callback must manage its own external effects, such as service configuration or database changes. Power loss and force-kill recovery are not a general transactional guarantee.

Keep private keys outside the checkout. RSA key generation is available without payment or an account. For Windows publisher identity, use your own certificate-store certificate with SignTool. Private certificates and passwords are not placed in installer recipes. Test keys do not establish public publisher trust.
