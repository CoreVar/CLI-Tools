namespace CoreVar.CommandLineInterface.Modules;

public sealed class ModuleCatalog
{
    public string SchemaVersion { get; init; } = "1.0";
    public List<ModuleCatalogEntry> Modules { get; init; } = [];
}

public sealed class ModuleCatalogEntry
{
    public required string Id { get; init; }
    public string? Description { get; init; }
    public List<ModuleRelease> Releases { get; init; } = [];
}

public sealed class ModuleRelease
{
    public required string Version { get; init; }
    public required Uri Package { get; init; }
    public required string Sha256 { get; init; }
    public string Channel { get; init; } = "stable";
    public bool Revoked { get; init; }
    public DateTimeOffset PublishedAt { get; init; }
}

public sealed class ModuleInstallationPointer
{
    public required string Version { get; init; }
    public string? PreviousVersion { get; init; }
    public string Channel { get; init; } = "stable";
    public Uri? Catalog { get; init; }
    public DateTimeOffset ActivatedAt { get; init; } = DateTimeOffset.UtcNow;
}
