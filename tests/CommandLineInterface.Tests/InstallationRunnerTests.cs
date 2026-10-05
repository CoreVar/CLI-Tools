using CoreVar.CommandLineInterface.Distribution;
using System.Text.Json;

namespace CommandLineInterface.Tests;

public sealed class InstallationRunnerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "installation-runner-" + Guid.NewGuid().ToString("N"));
    private readonly Guid _tenant = Guid.NewGuid(), _installation = Guid.NewGuid();
    private const string Secret = "synthetic-credential-must-not-appear";

    private InstallationRecipe Recipe(InstallationStep step, Guid? tenant = null, string? fingerprint = null, InstallationOperation operation = InstallationOperation.Install)
        => new() { Id = "sample", TenantId = tenant ?? _tenant, InstallationId = _installation,
            PlanFingerprint = fingerprint ?? new string('A', 64), Operation = operation, Steps = [step] };

    [Fact]
    public async Task InterruptedRegistrationReconcilesWithoutDuplicateApply()
    {
        var applied = false; var calls = 0; var keys = new List<Guid>();
        var resource = Guid.NewGuid();
        var step = new InstallationStep("register", (context, _) =>
        {
            keys.Add(context.StepOperationId);
            return ValueTask.FromResult(new InstallationProbe(applied ? InstallationProbeState.Applied : InstallationProbeState.NotApplied,
                applied ? new(resource, null, InstallationLifecycle.Registered, DateTimeOffset.UtcNow) : null));
        }, (_, _) => { calls++; applied = true; throw new InvalidOperationException(Secret); });
        var recipe = Recipe(step);
        var runner = new InstallationRunner(_root);
        var failure = await Assert.ThrowsAsync<InstallationRunException>(() => runner.RunAsync(recipe).AsTask());
        Assert.DoesNotContain(Secret, failure.ToString());
        Assert.Equal(InstallationStepStatus.Started, Diagnostic(await runner.ExportDiagnosticsAsync(recipe)).Steps[0].Status);
        var resumed = await new InstallationRunner(_root).RunAsync(recipe);
        Assert.Equal(1, calls);
        Assert.Single(keys.Distinct());
        Assert.Equal(InstallationStepStatus.Completed, resumed.Steps[0].Status);
        Assert.Equal(resource, resumed.Steps[0].Receipt!.ResourceId);
        Assert.DoesNotContain(Secret, await runner.ExportDiagnosticsAsync(recipe));
        Assert.DoesNotContain(Secret, await File.ReadAllTextAsync(runner.CheckpointPath));
    }

    [Fact]
    public async Task UnknownOutcomeNeverRetriesAndConfirmedAbsenceReusesOperationKey()
    {
        var state = InstallationProbeState.NotApplied; var calls = 0; Guid? key = null;
        var step = new InstallationStep("enroll", (_, _) => ValueTask.FromResult(new InstallationProbe(state)),
            (context, _) => { Assert.Equal(key ?? context.StepOperationId, context.StepOperationId); key = context.StepOperationId;
                calls++; state = InstallationProbeState.Unknown; throw new IOException(Secret); });
        var runner = new InstallationRunner(_root); var recipe = Recipe(step);
        await Assert.ThrowsAsync<InstallationRunException>(() => runner.RunAsync(recipe).AsTask());
        var uncertain = await Assert.ThrowsAsync<InstallationRunException>(() => runner.RunAsync(recipe).AsTask());
        Assert.Equal("reconciliation-required", uncertain.Code); Assert.Equal(1, calls);
        state = InstallationProbeState.NotApplied;
        await Assert.ThrowsAsync<InstallationRunException>(() => runner.RunAsync(recipe).AsTask());
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ChangedTenantPlanOrOperationIsRejectedBeforeCallbacks()
    {
        var calls = 0;
        var step = new InstallationStep("preflight", (_, _) => { calls++; return ValueTask.FromResult(new InstallationProbe(InstallationProbeState.Applied)); },
            (_, _) => ValueTask.CompletedTask);
        var runner = new InstallationRunner(_root);
        await runner.RunAsync(Recipe(step));
        foreach (var changed in new[] { Recipe(step, tenant: Guid.NewGuid()), Recipe(step, fingerprint: new string('B', 64)), Recipe(step, operation: InstallationOperation.Uninstall) })
        {
            var error = await Assert.ThrowsAsync<InstallationRunException>(() => runner.RunAsync(changed).AsTask());
            Assert.Equal("checkpoint-identity-mismatch", error.Code);
        }
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task CancellationPreservesStartedStateAndConcurrentRunIsExcluded()
    {
        using var cancellation = new CancellationTokenSource();
        var applied = false; var runner = new InstallationRunner(_root); InstallationRecipe recipe = null!;
        var step = new InstallationStep("configure", (_, _) => ValueTask.FromResult(new InstallationProbe(applied ? InstallationProbeState.Applied : InstallationProbeState.NotApplied)),
            async (_, token) =>
            {
                await Assert.ThrowsAsync<IOException>(() => new InstallationRunner(_root).RunAsync(recipe).AsTask());
                applied = true; cancellation.Cancel(); token.ThrowIfCancellationRequested();
            });
        recipe = Recipe(step);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(recipe, cancellation.Token).AsTask());
        Assert.Equal(InstallationStepStatus.Started, Diagnostic(await runner.ExportDiagnosticsAsync(recipe)).Steps[0].Status);
        Assert.Equal(InstallationStepStatus.Completed, (await runner.RunAsync(recipe)).Steps[0].Status);
    }

    [Fact]
    public async Task SuccessfulExitWithoutObservedEffectDoesNotCompleteStep()
    {
        var step = new InstallationStep("verify", (_, _) => ValueTask.FromResult(new InstallationProbe(InstallationProbeState.NotApplied)), (_, _) => ValueTask.CompletedTask);
        var runner = new InstallationRunner(_root); var recipe = Recipe(step);
        var failure = await Assert.ThrowsAsync<InstallationRunException>(() => runner.RunAsync(recipe).AsTask());
        Assert.Equal("verification-required", failure.Code);
        Assert.Equal(InstallationStepStatus.Started, Diagnostic(await runner.ExportDiagnosticsAsync(recipe)).Steps[0].Status);
    }

    [Fact]
    public async Task DiagnosticsDropUnknownFieldsAndUntrustedErrorText()
    {
        var step = new InstallationStep("check", (_, _) => ValueTask.FromResult(new InstallationProbe(InstallationProbeState.Applied)), (_, _) => ValueTask.CompletedTask);
        var recipe = Recipe(step); var runner = new InstallationRunner(_root);
        await runner.RunAsync(recipe);
        var json = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(runner.CheckpointPath))!;
        json["credential"] = Secret;
        json["steps"]![0]!["failureCode"] = Secret;
        await File.WriteAllTextAsync(runner.CheckpointPath, json.ToJsonString());
        var safe = await runner.ExportDiagnosticsAsync(recipe);
        Assert.DoesNotContain(Secret, safe);
        Assert.DoesNotContain("credential", safe);
        Assert.Equal("step-failed", Diagnostic(safe).Steps[0].FailureCode);
    }

    [Fact]
    public async Task LinkedCheckpointDoesNotReadOutsideData()
    {
        if (OperatingSystem.IsWindows()) return; // Windows symlink creation requires a host policy privilege.
        Directory.CreateDirectory(_root);
        var outside = Path.Combine(_root, "outside.json"); await File.WriteAllTextAsync(outside, Secret);
        var runner = new InstallationRunner(Path.Combine(_root, "operation")); Directory.CreateDirectory(runner.DirectoryPath);
        File.CreateSymbolicLink(runner.CheckpointPath, outside);
        var step = new InstallationStep("check", (_, _) => throw new Exception("Probe must not execute"), (_, _) => ValueTask.CompletedTask);
        var failure = await Assert.ThrowsAsync<InstallationRunException>(() => runner.RunAsync(Recipe(step)).AsTask());
        Assert.Equal("checkpoint-links-not-supported", failure.Code);
        Assert.Equal(Secret, await File.ReadAllTextAsync(outside));
    }

    [Fact]
    public async Task FailedReverificationDoesNotLeaveACompletedDiagnostic()
    {
        var state = InstallationProbeState.Applied;
        var step = new InstallationStep("reporting", (_, _) => ValueTask.FromResult(new InstallationProbe(state)), (_, _) => ValueTask.CompletedTask);
        var runner = new InstallationRunner(_root); var recipe = Recipe(step);
        await runner.RunAsync(recipe);
        state = InstallationProbeState.Unknown;
        await Assert.ThrowsAsync<InstallationRunException>(() => runner.RunAsync(recipe).AsTask());
        var diagnostics = Diagnostic(await runner.ExportDiagnosticsAsync(recipe));
        Assert.Equal(InstallationStepStatus.Started, diagnostics.Steps[0].Status);
        Assert.Equal("reconciliation-required", diagnostics.Steps[0].FailureCode);
    }

    [Fact]
    public async Task CleanInstallUpdateUninstallHooksAreRepeatable()
    {
        Directory.CreateDirectory(_root);
        var payload = Path.Combine(_root, "payload.txt");
        var unrelated = Path.Combine(_root, "keep.txt"); await File.WriteAllTextAsync(unrelated, "user data");
        foreach (var operation in Enum.GetValues<InstallationOperation>())
        {
            var desired = operation == InstallationOperation.Install ? "v1" : "v2";
            var calls = 0;
            var step = new InstallationStep("payload", (_, _) => ValueTask.FromResult(new InstallationProbe(
                (operation == InstallationOperation.Uninstall ? !File.Exists(payload) : File.Exists(payload) && File.ReadAllText(payload) == desired)
                    ? InstallationProbeState.Applied : InstallationProbeState.NotApplied)),
                async (_, token) => { calls++; if (operation == InstallationOperation.Uninstall) File.Delete(payload); else await File.WriteAllTextAsync(payload, desired, token); });
            var runner = new InstallationRunner(Path.Combine(_root, operation.ToString())); var recipe = Recipe(step, operation: operation);
            await runner.RunAsync(recipe); await runner.RunAsync(recipe); Assert.Equal(1, calls);
        }
        Assert.False(File.Exists(payload)); Assert.Equal("user data", await File.ReadAllTextAsync(unrelated));
    }

    private static InstallationCheckpoint Diagnostic(string json) => JsonSerializer.Deserialize(json, InstallationJsonContext.Default.InstallationCheckpoint)!;

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
