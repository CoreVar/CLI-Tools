using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoreVar.CommandLineInterface.Distribution;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(InstallationCheckpoint))]
public partial class InstallationJsonContext : JsonSerializerContext;
