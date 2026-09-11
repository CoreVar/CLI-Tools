using CoreVar.CommandLineInterface.Distribution;

namespace CommandLineInterface.Tests;

public sealed class ReleaseSetupCoordinatorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadinessFailureRollsBackWithIndependentCancellation(bool throws)
    {
        var current = State("1.0.0"); var updated = State("2.0.0", "1.0.0"); var rolledBack = false;
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<Exception>(() => ReleaseSetupCoordinator.RunAsync(current,
            (_, _) => ValueTask.FromResult(updated),
            (_, token) => { Assert.False(token.IsCancellationRequested); rolledBack = true; return ValueTask.FromResult(current); },
            (_, _) => throws ? ValueTask.FromException<bool>(new ApplicationException("setup")) : ValueTask.FromResult(false), cancellation.Token).AsTask());
        Assert.True(rolledBack);
    }

    [Fact]
    public async Task SameVersionStillRunsReadiness()
    {
        var current = State("1.0.0"); var called = false;
        await ReleaseSetupCoordinator.RunAsync(current, (_, _) => ValueTask.FromResult(current), (_, _) => ValueTask.FromResult(current),
            (_, _) => { called = true; return ValueTask.FromResult(true); });
        Assert.True(called);
    }

    [Fact]
    public async Task CancellationDuringReadinessStillRollsBack()
    {
        var current = State("1.0.0"); var updated = State("2.0.0", "1.0.0"); var rolledBack = false;
        await Assert.ThrowsAsync<OperationCanceledException>(() => ReleaseSetupCoordinator.RunAsync(current,
            (_, _) => ValueTask.FromResult(updated),
            (_, token) => { Assert.False(token.CanBeCanceled); rolledBack = true; return ValueTask.FromResult(current); },
            (_, _) => ValueTask.FromException<bool>(new OperationCanceledException())).AsTask());
        Assert.True(rolledBack);
    }

    private static InstallationState State(string version, string? previous = null) => new()
    { Product = "sample", Version = version, PreviousVersion = previous, Catalog = new("https://registry.example/catalog.json") };
}
