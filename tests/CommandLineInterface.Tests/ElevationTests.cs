using CoreVar.CommandLineInterface;
using CoreVar.CommandLineInterface.Elevation;
using Microsoft.Extensions.DependencyInjection;

namespace CommandLineInterface.Tests;

public sealed class ElevationTests
{
    [Fact]
    public async Task ContextProvidesSimpleElevationApi()
    {
        var service = new RecordingElevationService();
        using var services = new ServiceCollection().AddSingleton<IElevationService>(service).BuildServiceProvider();
        var context = new CommandExecutionContext(services, null!);

        var result = await context.RunElevatedAsync(
            "tool",
            ["argument with spaces", "--flag"],
            workingDirectory: "working",
            waitForExit: false);

        Assert.True(result.Succeeded);
        Assert.Equal("tool", service.Request!.FileName);
        Assert.Equal(["argument with spaces", "--flag"], service.Request.Arguments);
        Assert.Equal("working", service.Request.WorkingDirectory);
        Assert.False(service.Request.WaitForExit);
    }

    [Fact]
    public void CanceledElevationIsNotSuccessful()
    {
        var result = ElevationResult.UserCanceled();

        Assert.True(result.Canceled);
        Assert.False(result.Started);
        Assert.False(result.Succeeded);
    }

    private sealed class RecordingElevationService : IElevationService
    {
        public bool IsElevated => false;
        public ElevationRequest? Request { get; private set; }

        public ValueTask<ElevationResult> RunAsync(ElevationRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return ValueTask.FromResult(new ElevationResult(true, false, null));
        }

        public ValueTask<ElevationResult> RelaunchAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new ElevationResult(true, false, null));
    }
}
