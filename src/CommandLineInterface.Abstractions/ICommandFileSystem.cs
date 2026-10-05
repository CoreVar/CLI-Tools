namespace CoreVar.CommandLineInterface;

/// <summary>Portable command files. Browser paths identify cached files, never server filesystem paths.</summary>
public interface ICommandFileSystem
{
    ValueTask<string> ResolveReadPathAsync(CommandFileRequest request, CancellationToken cancellationToken = default);
    ValueTask<Stream> OpenReadAsync(CommandFileRequest request, CancellationToken cancellationToken = default);
    ValueTask<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default)
        => OpenReadAsync(new CommandFileRequest(path), cancellationToken);
    ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);
    ValueTask WriteAllBytesAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>A read request. SuggestedPath gives omitted filename inputs a stable browser-cache identity.</summary>
public sealed record CommandFileRequest(string? Path = null)
{
    public string? Label { get; init; }
    public string? SuggestedPath { get; init; }
    public bool AllowInteraction { get; init; } = true;
}
