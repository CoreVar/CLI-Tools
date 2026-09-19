using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CoreVar.CommandLineInterface.Elevation;

/// <summary>Uses the operating system's native privilege-elevation mechanism.</summary>
public sealed class ProcessElevationService : IElevationService
{
    private const int ErrorCancelled = 1223;
    private const uint TokenQuery = 0x0008;
    private const int TokenElevation = 20;

    public bool IsElevated => OperatingSystem.IsWindows() ? IsWindowsElevated() : GetEffectiveUserId() == 0;

    public ValueTask<ElevationResult> RelaunchAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("The current process path is unavailable.");
        IReadOnlyList<string> relaunchArguments = arguments;

        if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var originalEntryPoint = Environment.GetCommandLineArgs().FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(originalEntryPoint)
                && !string.Equals(Path.GetFullPath(originalEntryPoint), Path.GetFullPath(executable), StringComparison.OrdinalIgnoreCase))
                relaunchArguments = [originalEntryPoint, .. arguments];
        }

        return RunAsync(new(executable, relaunchArguments)
        {
            WorkingDirectory = Environment.CurrentDirectory
        }, cancellationToken);
    }

    public async ValueTask<ElevationResult> RunAsync(
        ElevationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        ArgumentNullException.ThrowIfNull(request.Arguments);

        try
        {
            using var process = Process.Start(CreateStartInfo(request));
            if (process is null)
                return new(false, false, null, "The elevated process could not be started.");

            if (!request.WaitForExit)
                return new(true, false, null);

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return new(true, false, process.ExitCode);
        }
        catch (Win32Exception exception) when (OperatingSystem.IsWindows() && exception.NativeErrorCode == ErrorCancelled)
        {
            return ElevationResult.UserCanceled();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return new(false, false, null, exception.Message);
        }
    }

    private ProcessStartInfo CreateStartInfo(ElevationRequest request)
    {
        ProcessStartInfo startInfo;

        if (IsElevated)
        {
            startInfo = new(request.FileName) { UseShellExecute = false };
            AddArguments(startInfo, request.Arguments);
        }
        else if (OperatingSystem.IsWindows())
        {
            startInfo = new(request.FileName)
            {
                UseShellExecute = true,
                Verb = "runas"
            };
            AddArguments(startInfo, request.Arguments);
        }
        else
        {
            startInfo = new("sudo") { UseShellExecute = false };
            startInfo.ArgumentList.Add("--");
            startInfo.ArgumentList.Add(request.FileName);
            AddArguments(startInfo, request.Arguments);
        }

        startInfo.WorkingDirectory = request.WorkingDirectory ?? Environment.CurrentDirectory;
        return startInfo;
    }

    private static void AddArguments(ProcessStartInfo startInfo, IEnumerable<string> arguments)
    {
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
    }

    private static bool IsWindowsElevated()
    {
        if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TokenQuery, out var token))
            return false;

        try
        {
            var elevation = new TokenElevationInfo();
            var size = Marshal.SizeOf<TokenElevationInfo>();
            return GetTokenInformation(token, TokenElevation, ref elevation, size, out _)
                && elevation.TokenIsElevated != 0;
        }
        finally
        {
            _ = CloseHandle(token);
        }
    }

    private static uint GetEffectiveUserId()
    {
        try
        {
            return geteuid();
        }
        catch (DllNotFoundException)
        {
            return uint.MaxValue;
        }
        catch (EntryPointNotFoundException)
        {
            return uint.MaxValue;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenElevationInfo
    {
        public int TokenIsElevated;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(
        IntPtr tokenHandle,
        int tokenInformationClass,
        ref TokenElevationInfo tokenInformation,
        int tokenInformationLength,
        out int returnLength);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("libc")]
    private static extern uint geteuid();
}
