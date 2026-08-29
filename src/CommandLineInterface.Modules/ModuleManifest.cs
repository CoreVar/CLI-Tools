using System.Text.Json.Serialization;

namespace CoreVar.CommandLineInterface.Modules;

/// <summary>Describes an installable, language-neutral CLI module.</summary>
public sealed class ModuleManifest
{
    public const string CurrentSchema = "1.0";
    public const string ProcessProtocol = "corevar.module.process/1";

    public string SchemaVersion { get; init; } = CurrentSchema;
    public required string Id { get; init; }
    public required string Version { get; init; }
    public string? Description { get; init; }
    public string? CliCompatibility { get; init; }
    public ModuleRuntime Runtime { get; init; } = new();
    public Dictionary<string, ModuleEntrypoint> Entrypoints { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ModuleCommand> Commands { get; init; } = [];
    public Dictionary<string, string> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    [JsonIgnore] public string InstallDirectory { get; internal set; } = string.Empty;
}

public sealed class ModuleRuntime
{
    public ModuleRuntimeKind Kind { get; init; } = ModuleRuntimeKind.Native;
    public ModuleRuntimeProvisioning Provisioning { get; init; } = ModuleRuntimeProvisioning.System;
    public string? Version { get; init; }
    public string? Executable { get; init; }
    public string? RequirementsFile { get; init; }
    public string? PackageDirectory { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<ModuleRuntimeKind>))]
public enum ModuleRuntimeKind { Native, DotNet, Python, Node }

[JsonConverter(typeof(JsonStringEnumConverter<ModuleRuntimeProvisioning>))]
public enum ModuleRuntimeProvisioning { System, Bundled }

public sealed class ModuleEntrypoint
{
    public required string Path { get; init; }
    public string? Interpreter { get; init; }
    public List<string> Arguments { get; init; } = [];
}

public sealed class ModuleCommand
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool Hidden { get; init; }
    public List<string> Aliases { get; init; } = [];
    public List<ModuleCommand> Commands { get; init; } = [];
    public List<ModuleOption> Options { get; init; } = [];
    public List<ModuleArgument> Arguments { get; init; } = [];
}

public sealed class ModuleOption
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public List<string> Aliases { get; init; } = [];
    public bool RequiresValue { get; init; }
    public List<string> Completions { get; init; } = [];
}

public sealed class ModuleArgument
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool Required { get; init; }
    public bool Remaining { get; init; }
    public List<string> Completions { get; init; } = [];
}
