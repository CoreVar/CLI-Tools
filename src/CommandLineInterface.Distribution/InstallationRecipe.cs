namespace CoreVar.CommandLineInterface.Distribution;

public enum InstallationOperation { Install, Update, Uninstall }
public enum InstallationProbeState { Unknown, NotApplied, Applied }
public enum InstallationLifecycle { Applied, Registered, CredentialsIssued, Connected, Reporting, Operational, Removed }
public enum InstallationStepStatus { Pending, Started, Completed }

/// <summary>Only identifiers and observed lifecycle state belong in durable receipts.</summary>
public sealed record InstallationReceipt(Guid? ResourceId, Guid? DeploymentId, InstallationLifecycle Lifecycle, DateTimeOffset ObservedAt);
public sealed record InstallationProbe(InstallationProbeState State, InstallationReceipt? Receipt = null);

public sealed record InstallationStepContext(Guid TenantId, Guid InstallationId, Guid OperationId,
    Guid StepOperationId, string StepId, string PlanFingerprint, bool WasStarted);

/// <summary>Probe must distinguish confirmed absence from an uncertain remote outcome.</summary>
public sealed record InstallationStep(string Id,
    Func<InstallationStepContext, CancellationToken, ValueTask<InstallationProbe>> ProbeAsync,
    Func<InstallationStepContext, CancellationToken, ValueTask> ApplyAsync);

public sealed class InstallationRecipe
{
    public required string Id { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid InstallationId { get; init; }
    /// <summary>SHA-256 of canonical, non-secret plan inputs, including exact product/artifact versions.</summary>
    public required string PlanFingerprint { get; init; }
    public InstallationOperation Operation { get; init; }
    public required IReadOnlyList<InstallationStep> Steps { get; init; }
}

/// <summary>A safe error code; raw callback exceptions and child output are never included.</summary>
public sealed class InstallationRunException(string code, string? stepId = null)
    : InvalidOperationException(stepId is null ? $"Installation stopped: {code}." : $"Installation step '{stepId}' stopped: {code}.")
{
    public string Code { get; } = code;
    public string? StepId { get; } = stepId;
}

public sealed class InstallationCheckpoint
{
    public int SchemaVersion { get; init; } = 1;
    public required string RecipeId { get; init; }
    public Guid TenantId { get; init; }
    public Guid InstallationId { get; init; }
    public Guid OperationId { get; init; }
    public InstallationOperation Operation { get; init; }
    public required string PlanFingerprint { get; init; }
    public List<InstallationStepCheckpoint> Steps { get; init; } = [];
}

public sealed class InstallationStepCheckpoint
{
    public required string Id { get; init; }
    public Guid OperationId { get; init; }
    public InstallationStepStatus Status { get; set; }
    public InstallationReceipt? Receipt { get; set; }
    public string? FailureCode { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
