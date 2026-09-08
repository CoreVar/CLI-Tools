namespace CoreVar.CommandLineInterface.Modules;

/// <summary>Per-execution services, streams, credentials, and safety limits for an external module.</summary>
public sealed record ModuleInvocationContext(IServiceProvider Services, IConsoleControl Console)
{
    public IModuleCredentialProvider? CredentialProvider { get; init; }
    public IReadOnlyDictionary<string, string?> Environment { get; init; } = new Dictionary<string, string?>();
    public Func<CancellationToken, ValueTask<string?>>? StandardInput { get; init; }
    public int MaximumOutputCharacters { get; init; } = 1_000_000;
}

/// <summary>Resolves short-lived credentials for one module invocation.</summary>
public interface IModuleCredentialProvider
{
    ValueTask<IReadOnlyDictionary<string, string?>> GetEnvironmentAsync(ModuleManifest module,
        IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}
