namespace CoreVar.CommandLineInterface.Distribution;

public sealed record NativeUpdateCommand(string Executable, IReadOnlyList<string> Arguments, string Explanation);

public static class NativeUpdateCommands
{
    public static NativeUpdateCommand? Create(InstallationState state) => state.Provider switch
    {
        InstallationProvider.DotNetTool => new("dotnet", ["tool", "update", "--global", RequiredPackage(state)], "The .NET SDK owns this installation."),
        InstallationProvider.WinGet => new("winget", ["upgrade", "--id", RequiredPackage(state), "--exact"], "winget owns this installation."),
        InstallationProvider.Homebrew => new("brew", ["upgrade", RequiredPackage(state)], "Homebrew owns this installation."),
        InstallationProvider.Apt => new("sudo", ["apt-get", "install", "--only-upgrade", RequiredPackage(state)], "APT owns this installation."),
        InstallationProvider.Rpm => new("sudo", ["dnf", "upgrade", RequiredPackage(state)], "RPM/DNF owns this installation."),
        _ => null
    };

    private static string RequiredPackage(InstallationState state) => state.PackageId
        ?? throw new InvalidOperationException($"Installation provider '{state.Provider}' requires a package ID.");
}
