using System.Text.Json;
using CoreVar.CommandLineInterface.IO;

namespace CoreVar.CommandLineInterface.Distribution;

/// <summary>Durable recipe execution. Use a separate checkpoint directory per operation/plan.</summary>
public sealed class InstallationRunner(string checkpointDirectory)
{
    public string DirectoryPath { get; } = Path.GetFullPath(checkpointDirectory);
    public string CheckpointPath => Path.Combine(DirectoryPath, "checkpoint.json");

    /// <summary>Exports only the validated identity, step states, typed receipts and allowlisted failure codes.</summary>
    public async ValueTask<string> ExportDiagnosticsAsync(InstallationRecipe recipe, CancellationToken cancellationToken = default)
    {
        ValidateRecipe(recipe);
        RejectLinks(DirectoryPath);
        RejectLinks(Path.Combine(DirectoryPath, ".operation.lock"));
        using var operation = InstallationFiles.AcquireLock(DirectoryPath);
        RejectLinks(CheckpointPath);
        InstallationCheckpoint checkpoint;
        try
        {
            await using var stream = File.OpenRead(CheckpointPath);
            checkpoint = await JsonSerializer.DeserializeAsync(stream, InstallationJsonContext.Default.InstallationCheckpoint, cancellationToken)
                ?? throw new InstallationRunException("checkpoint-invalid");
        }
        catch (JsonException) { throw new InstallationRunException("checkpoint-invalid"); }
        ValidateIdentity(recipe, checkpoint);
        foreach (var step in checkpoint.Steps)
        {
            if (step.FailureCode is not null and not ("cancelled" or "step-failed" or "reconciliation-required" or "verification-required" or "probe-invalid" or "receipt-invalid"))
                step.FailureCode = "step-failed";
        }
        return JsonSerializer.Serialize(checkpoint, InstallationJsonContext.Default.InstallationCheckpoint);
    }

    public async ValueTask<InstallationCheckpoint> RunAsync(InstallationRecipe recipe, CancellationToken cancellationToken = default)
    {
        ValidateRecipe(recipe);
        RejectLinks(DirectoryPath);
        RejectLinks(Path.Combine(DirectoryPath, ".operation.lock"));
        using var operation = InstallationFiles.AcquireLock(DirectoryPath);
        RejectLinks(CheckpointPath);
        InstallationCheckpoint checkpoint;
        if (File.Exists(CheckpointPath))
        {
            try
            {
                await using var stream = File.OpenRead(CheckpointPath);
                checkpoint = await JsonSerializer.DeserializeAsync(stream, InstallationJsonContext.Default.InstallationCheckpoint, cancellationToken)
                    ?? throw new InstallationRunException("checkpoint-invalid");
            }
            catch (JsonException) { throw new InstallationRunException("checkpoint-invalid"); }
            ValidateIdentity(recipe, checkpoint);
        }
        else
        {
            checkpoint = new InstallationCheckpoint
            {
                RecipeId = recipe.Id, TenantId = recipe.TenantId, InstallationId = recipe.InstallationId,
                OperationId = Guid.NewGuid(), Operation = recipe.Operation, PlanFingerprint = recipe.PlanFingerprint.ToUpperInvariant(),
                Steps = recipe.Steps.Select(step => new InstallationStepCheckpoint { Id = step.Id, OperationId = Guid.NewGuid() }).ToList()
            };
            await SaveAsync(checkpoint);
        }

        for (var index = 0; index < recipe.Steps.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = recipe.Steps[index];
            var saved = checkpoint.Steps[index];
            var context = new InstallationStepContext(recipe.TenantId, recipe.InstallationId, checkpoint.OperationId,
                saved.OperationId, step.Id, checkpoint.PlanFingerprint, saved.Status != InstallationStepStatus.Pending);
            try
            {
                var probe = await step.ProbeAsync(context, cancellationToken);
                if (probe.State == InstallationProbeState.Unknown) throw new InstallationRunException("reconciliation-required", step.Id);
                if (probe.State == InstallationProbeState.NotApplied)
                {
                    saved.Status = InstallationStepStatus.Started;
                    saved.Receipt = null;
                    saved.FailureCode = null;
                    saved.UpdatedAt = DateTimeOffset.UtcNow;
                    await SaveAsync(checkpoint); // Persist the idempotency key and uncertain state before side effects.
                    cancellationToken.ThrowIfCancellationRequested();
                    await step.ApplyAsync(context with { WasStarted = true }, cancellationToken);
                    probe = await step.ProbeAsync(context with { WasStarted = true }, cancellationToken);
                    if (probe.State != InstallationProbeState.Applied) throw new InstallationRunException("verification-required", step.Id);
                }
                if (probe.State != InstallationProbeState.Applied) throw new InstallationRunException("probe-invalid", step.Id);
                if (probe.Receipt is { } receipt && !Enum.IsDefined(receipt.Lifecycle)) throw new InstallationRunException("receipt-invalid", step.Id);
                saved.Status = InstallationStepStatus.Completed;
                saved.Receipt = probe.Receipt;
                saved.FailureCode = null;
                saved.UpdatedAt = DateTimeOffset.UtcNow;
                await SaveAsync(checkpoint);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (saved.Status == InstallationStepStatus.Completed) saved.Status = InstallationStepStatus.Started;
                saved.FailureCode = "cancelled";
                saved.UpdatedAt = DateTimeOffset.UtcNow;
                await SaveAsync(checkpoint); // Cancellation must not erase evidence of an operation already started.
                throw new OperationCanceledException(cancellationToken);
            }
            catch (Exception exception)
            {
                if (saved.Status == InstallationStepStatus.Completed) saved.Status = InstallationStepStatus.Started;
                saved.FailureCode = exception is InstallationRunException run &&
                    run.Code is "reconciliation-required" or "verification-required" or "probe-invalid" or "receipt-invalid"
                    ? run.Code : "step-failed";
                saved.UpdatedAt = DateTimeOffset.UtcNow;
                await SaveAsync(checkpoint);
                throw new InstallationRunException(saved.FailureCode, step.Id);
            }
        }
        return checkpoint;
    }

    private async ValueTask SaveAsync(InstallationCheckpoint checkpoint)
    {
        var temporary = Path.Combine(DirectoryPath, ".checkpoint-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, checkpoint, InstallationJsonContext.Default.InstallationCheckpoint, CancellationToken.None);
                stream.Flush(true);
            }
            File.Move(temporary, CheckpointPath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void ValidateRecipe(InstallationRecipe recipe)
    {
        static bool Identifier(string value) => !string.IsNullOrEmpty(value) && value.Length <= 100 &&
            value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
        if (!Identifier(recipe.Id) || recipe.TenantId == Guid.Empty || recipe.InstallationId == Guid.Empty || !Enum.IsDefined(recipe.Operation) ||
            recipe.PlanFingerprint is null || recipe.PlanFingerprint.Length != 64 || !recipe.PlanFingerprint.All(Uri.IsHexDigit) ||
            recipe.Steps is null || recipe.Steps.Count is < 1 or > 256 || recipe.Steps.Any(step => step is null || !Identifier(step.Id) || step.ProbeAsync is null || step.ApplyAsync is null) ||
            recipe.Steps.Select(step => step.Id).Distinct(StringComparer.Ordinal).Count() != recipe.Steps.Count)
            throw new InstallationRunException("recipe-invalid");
    }

    private static void ValidateIdentity(InstallationRecipe recipe, InstallationCheckpoint checkpoint)
    {
        if (checkpoint.SchemaVersion != 1 || checkpoint.RecipeId != recipe.Id || checkpoint.TenantId != recipe.TenantId ||
            checkpoint.InstallationId != recipe.InstallationId || checkpoint.Operation != recipe.Operation || checkpoint.OperationId == Guid.Empty ||
            !string.Equals(checkpoint.PlanFingerprint, recipe.PlanFingerprint, StringComparison.OrdinalIgnoreCase) ||
            checkpoint.Steps is null || !checkpoint.Steps.Select(step => step?.Id).SequenceEqual(recipe.Steps.Select(step => step.Id)) ||
            checkpoint.Steps.Any(step => step is null || step.OperationId == Guid.Empty || !Enum.IsDefined(step.Status) ||
                (step.Receipt is { } receipt && !Enum.IsDefined(receipt.Lifecycle))))
            throw new InstallationRunException("checkpoint-identity-mismatch");
    }

    private static void RejectLinks(string path)
    {
        // The caller owns the parent location; OS temp paths may legitimately have
        // symlink ancestors (for example /var on macOS). Never follow a substituted
        // checkpoint directory, checkpoint file or lock file itself.
        if (Path.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InstallationRunException("checkpoint-links-not-supported");
    }
}
