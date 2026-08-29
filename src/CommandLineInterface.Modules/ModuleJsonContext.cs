using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoreVar.CommandLineInterface.Modules;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true,
    PropertyNameCaseInsensitive = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(ModuleManifest))]
[JsonSerializable(typeof(ModuleInstallationPointer))]
[JsonSerializable(typeof(ModuleCatalog))]
public partial class ModuleJsonContext : JsonSerializerContext;
