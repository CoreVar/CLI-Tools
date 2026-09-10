using System.Security.Cryptography;
using System.Text;
using CoreVar.CommandLineInterface.Distribution;

namespace CommandLineInterface.Tests;

public sealed class ProtectedProcessHandoffTests
{
    private static string Shell => OperatingSystem.IsWindows()
        ? (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Select(path => Path.Combine(path, "pwsh.exe")).First(File.Exists)
        : "/bin/sh";

    private static string[] Arguments(string windows, string unix) => OperatingSystem.IsWindows()
        ? ["-NoProfile", "-NonInteractive", "-Command", windows] : ["-c", unix];

    [Fact]
    public async Task CredentialEnvelopeUsesStdinAndNoisyChildCannotDeadlockOrExposeIt()
    {
        var envelope = Encoding.UTF8.GetBytes(new string('x', 65536));
        var arguments = Arguments(
            "$value=[Console]::In.ReadToEnd();[Console]::Out.Write($value);[Console]::Error.Write($value);if($value.Length -eq 65536){exit 17}else{exit 2}",
            "value=$(cat); printf '%s' \"$value\"; printf '%s' \"$value\" >&2; test \"${#value}\" -eq 65536 && exit 17; exit 2");
        try
        {
            Assert.All(arguments, argument => Assert.DoesNotContain(Encoding.UTF8.GetString(envelope), argument));
            var result = await ProtectedProcessHandoff.RunAsync(Shell, arguments, envelope, TimeSpan.FromSeconds(20));
            Assert.Equal(17, result);
        }
        finally { CryptographicOperations.ZeroMemory(envelope); }
    }

    [Fact]
    public async Task UnresponsiveChildIsBoundedAndErrorsAreSecretFree()
    {
        var envelope = Encoding.UTF8.GetBytes("synthetic-secret-never-log");
        try
        {
            var failure = await Assert.ThrowsAsync<InstallationRunException>(() => ProtectedProcessHandoff.RunAsync(Shell,
                Arguments("Start-Sleep -Seconds 30", "sleep 30"), envelope, TimeSpan.FromMilliseconds(300)).AsTask());
            Assert.Equal("handoff-timeout", failure.Code);
            Assert.DoesNotContain("synthetic-secret", failure.ToString());
        }
        finally { CryptographicOperations.ZeroMemory(envelope); }
    }

    [Fact]
    public async Task CancelledHandoffNeverStartsAndOversizeEnvelopeIsRejected()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ProtectedProcessHandoff.RunAsync(Shell,
            Arguments("exit 99", "exit 99"), new byte[1], TimeSpan.FromSeconds(1), cancellationToken: cancellation.Token).AsTask());
        var failure = await Assert.ThrowsAsync<InstallationRunException>(() => ProtectedProcessHandoff.RunAsync(Shell, [],
            new byte[1_048_577], TimeSpan.FromSeconds(1)).AsTask());
        Assert.Equal("handoff-invalid", failure.Code);
    }
}
