using CoreVar.CommandLineInterface.Registry;
using Xunit;

namespace CommandLineInterface.Registry.Tests;

public sealed class CompleteReleaseTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cli-registry-complete", Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task ValidatesRidMatrixBeforePromotion()
    {
        var store = new FileRegistryStore(new RegistryOptions { DataRoot = _root });
        await store.PublishReleaseAsync("sample", "sample-cli", "1.0.0", "linux-x64", "candidate", new MemoryStream([1]), new Uri("https://registry.example/linux.zip"), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CompleteReleaseAsync("sample", "sample-cli", "1.0.0", ["linux-x64", "win-x64"], null, [], "stable", new(1, 0), new(10, 0), default).AsTask());
        await store.PublishReleaseAsync("sample", "sample-cli", "1.0.0", "win-x64", "candidate", new MemoryStream([2]), new Uri("https://registry.example/windows.zip"), default);
        await store.CompleteReleaseAsync("sample", "sample-cli", "1.0.0", ["linux-x64", "win-x64"], null, ["setup"], "stable", new(1, 0), new(10, 0), default);
        var catalog = await store.GetReleaseCatalogAsync("sample", "sample-cli", default);
        Assert.Equal("1.0.0", catalog!.Channels["stable"]);
        Assert.Equal(["setup"], Assert.Single(catalog.Releases).PostInstallArguments);
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
