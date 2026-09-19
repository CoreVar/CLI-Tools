# Resumable installation primitives

`CoreVar.CommandLineInterface.Distribution` provides generic installation execution. Product selection, Compose fragments, registration/enrollment adapters, authorization, environment preflight, connection checks and cleanup policy remain in the consuming recipe. CoreVar Global CLI owns the CoreOne recipe.

## Recipe and checkpoint identity

Create an `InstallationRecipe` with a static recipe `Id`, tenant and installation GUIDs, `InstallationOperation.Install`, `Update` or `Uninstall`, an ordered step list, and a 64-character SHA-256 `PlanFingerprint`. Hash canonical **non-secret** plan inputs: tenant/installation scope, selected capabilities, resolved dependencies, exact compatibility manifest, image/fragment digests and recipe revision. Never use credentials as identifiers or fingerprint inputs.

Create `InstallationRunner` with a dedicated checkpoint directory for that operation and plan, under a parent location controlled by the installing user. The checkpoint directory, journal and lock cannot themselves be links; parent path ownership remains the caller's responsibility. Keep it separate from a direct updater's root lock; a recipe may invoke the updater from a step. A checkpoint from another tenant, installation, fingerprint, operation, recipe or ordered step list is rejected before callbacks execute. Use a new directory for a deliberately changed plan; do not delete an uncertain operation's checkpoint to bypass reconciliation.

Each checkpoint has a durable operation ID and each step a durable `StepOperationId`. Pass that step key as the downstream operation/idempotency key, scoped to the tenant and installation. An exclusive filesystem lock covers probes, side effects and checkpoint commits. Flush-and-rename persists checkpoint updates; cancellation does not cancel recording a started effect. This is a local-filesystem contract, not a distributed lock across machines or an exactly-once remote transaction. Network filesystems and power-loss durability require target qualification.

## Probe, apply, verify

An `InstallationStep` has `ProbeAsync` and `ApplyAsync` delegates accepting `InstallationStepContext` and a cancellation token. Probes run before applying, after applying, and again on resumed/completed runs:

- `Applied`: the required effect is separately observed. Skip apply and persist the optional typed receipt.
- `NotApplied`: the recipe has positively established absence, or its downstream idempotency contract makes retry safe. Apply using the same persisted step operation key, then probe again.
- `Unknown`: the outcome cannot safely be established. Stop with `reconciliation-required`; never blindly repeat registration or another side effect.

After apply, anything other than `Applied` stops with `verification-required`. A successful child exit or an accepted remote command is insufficient. `WasStarted` tells an adapter the checkpoint may represent an earlier attempt. A crash after the remote effect but before local completion must be resolved through the downstream operation key and authoritative state.

`InstallationReceipt` contains only resource/deployment GUIDs, an `InstallationLifecycle` enum and observation time. `Registered`, `CredentialsIssued`, `Connected`, `Reporting` and `Operational` are distinct values; the recipe must enforce the lifecycle needed by each step. The generic runner does not treat credentials issued as connectivity or reporting. Install, update and uninstall use the same step protocol; the consumer supplies capability-specific callbacks and retention policy. Checkpoints are retained as evidence after uninstall.

## Protected credential handoff

`ProtectedProcessHandoff.RunAsync(absoluteExecutable, nonSecretArguments, credentialEnvelope, timeout, workingDirectory, cancellationToken)` sends one envelope of at most 1 MiB to a trusted adapter through an anonymous stdin pipe, then closes stdin. It returns only the exit code. It does not put credentials in argv, environment variables, journal files or diagnostic output. It drains and discards both child output streams, including on error, and attempts to terminate the running process tree on cancellation/timeout.

The caller owns the byte buffer and should clear it in `finally` with `CryptographicOperations.ZeroMemory`. Arguments and working-directory paths must be non-secret. The child must explicitly implement this stdin/EOF protocol, handle its own protected runtime storage and remain attached until its work finishes. This is not an OS vault, an isolation boundary from the same OS user/administrator, a remote secure channel, or proof an enrollment succeeded. It cannot revoke credentials already delivered; reconcile remote state after an uncertain handoff. Existing product services are not assumed to accept stdin. Do not substitute command-line arguments when no adapter exists.

## Diagnostics

`ExportDiagnosticsAsync(recipe)` validates the checkpoint's identity, reserializes known typed fields, and emits only allowlisted failure codes. It does not include callback exception messages, inner exceptions, child stdout/stderr, raw command arguments, environment/configuration, or arbitrary receipt metadata. Consumer logging must preserve that boundary. Detailed product diagnostics require their own redaction and authorization contract.

## Acceptance evidence — 2026-09-10

Status: **implemented and cross-platform CI verified**, not live CoreOne or release qualified.

Implementation commit: `bba914bf17d3ce499ef589cf8a6cadcf96b1f41b`. [GitHub run 34454140365](https://github.com/CoreVar/CLI-Tools/actions/runs/34454140365) passed browser, Ubuntu, macOS and Windows jobs, including signed distribution acceptance, native AOT, and Windows install/repair/upgrade/uninstall. The new Unix checkpoint-link case ran on Ubuntu/macOS. See [the acceptance ledger](coreone-installer-acceptance.json) for the pinned source and remaining integration gates.

The first acceptance pass covered nine new tests on both .NET 8 and .NET 10 on Windows. Final local solution validation passed 82 tests on each framework and eight registry tests. Later coverage adds re-verification, diagnostic redaction, and a Unix-only checkpoint-link regression (not exercised on Windows):

```console
dotnet test tests/CommandLineInterface.Tests/CommandLineInterface.Tests.csproj -c Release --filter "FullyQualifiedName~InstallationRunnerTests|FullyQualifiedName~ProtectedProcessHandoffTests"
```

Coverage: a registration effect followed by a lost response resumes without duplicate apply; uncertain outcomes stop; confirmed absence retries with the original key; tenant/plan/operation changes fail closed; cancellation retains started state; concurrent runners cannot enter; unobserved effects do not complete; local fixture install/update/uninstall is repeatable and preserves unrelated data; a child reads its envelope and emits more than pipe capacity without exposing output; timeout/cancellation and envelope bounds are enforced.

TRX output is retained locally under `.artifacts/coreone-installer/` (01:12 run on 2026-09-10). GitHub validation retains platform-specific TRX logs as workflow artifacts. The source revision is the commit containing this implementation and its acceptance tests; no new NuGet package has been released from this work.

The lifecycle fixture uses isolated local files. It does not qualify actual product installation, Compose upgrades, service credentials, remote cleanup, physical devices, cloud targets, or power-loss behavior. Existing signed distribution and native Windows lifecycle tests remain separate evidence. CoreVar Global CLI must supply the exact recipe, runtime digests, trusted enrollment adapters and observed-state probes; the delivery coordinator owns integrated clean assembly and M1 qualification. No deployment, resource creation or paid qualification was performed by this change.
