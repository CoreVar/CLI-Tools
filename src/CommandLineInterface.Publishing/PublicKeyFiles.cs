using System.Text.Json;

namespace CoreVar.CommandLineInterface.Publishing;

public static class PublicKeyFiles
{
    /// <summary>Reads a JSON object mapping key IDs to public PEM keys without reflection.</summary>
    public static Dictionary<string, string> Load(string path)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        return json.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()
            ?? throw new InvalidDataException("Public keys must be PEM strings."), StringComparer.Ordinal);
    }
}
