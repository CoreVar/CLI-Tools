using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoreVar.CommandLineInterface.Distribution;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true, PropertyNameCaseInsensitive = true, UseStringEnumConverter = true,
    Converters = new[] { typeof(CoreVar.CommandLineInterface.IO.PortableUriJsonConverter) })]
[JsonSerializable(typeof(ReleaseCatalog))]
[JsonSerializable(typeof(InstallationState))]
[JsonSerializable(typeof(ReleaseBundleBootstrap))]
[JsonSerializable(typeof(ReleaseMetadataPayload))]
public partial class DistributionJsonContext : JsonSerializerContext;
