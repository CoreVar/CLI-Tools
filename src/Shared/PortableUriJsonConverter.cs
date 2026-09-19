using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoreVar.CommandLineInterface.IO;

// System.Text.Json normally writes Uri.OriginalString. For a URI constructed
// from a Unix file path that loses the file scheme on deserialization.
internal sealed class PortableUriJsonConverter : JsonConverter<Uri>
{
    public override Uri Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString()!, UriKind.RelativeOrAbsolute);

    public override void Write(Utf8JsonWriter writer, Uri value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.IsAbsoluteUri ? value.AbsoluteUri : value.OriginalString);
}
