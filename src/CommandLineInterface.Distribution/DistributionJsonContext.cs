using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoreVar.CommandLineInterface.Distribution;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true, PropertyNameCaseInsensitive = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(ReleaseCatalog))]
[JsonSerializable(typeof(InstallationState))]
[JsonSerializable(typeof(ReleaseBundleBootstrap))]
public partial class DistributionJsonContext : JsonSerializerContext;
