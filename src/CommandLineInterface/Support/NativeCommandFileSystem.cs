namespace CoreVar.CommandLineInterface.Support;

/// <summary>Native disk implementation of the portable command-file API.</summary>
public sealed class NativeCommandFileSystem(ICommandPromptService prompt) : ICommandFileSystem
{
    public async ValueTask<string> ResolveReadPathAsync(CommandFileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var path = request.Path;
        if (path is null)
        {
            if (!request.AllowInteraction || !prompt.IsInteractive) throw new CommandPromptException();
            path = await prompt.ReadAsync(new CommandPromptRequest(request.Label ?? "Input file"), cancellationToken);
            if (string.IsNullOrWhiteSpace(path)) throw new CommandPromptException();
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(path)) throw new FileNotFoundException("The requested input file does not exist.", path);
        return path;
    }

    public async ValueTask<Stream> OpenReadAsync(CommandFileRequest request, CancellationToken cancellationToken = default)
        => new FileStream(await ResolveReadPathAsync(request, cancellationToken), FileMode.Open, FileAccess.Read,
            FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);

    public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(File.Exists(path));
    }
    public async ValueTask WriteAllBytesAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
        await stream.WriteAsync(content, cancellationToken);
    }
    public ValueTask<bool> DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var exists = File.Exists(path);
        File.Delete(path);
        return ValueTask.FromResult(exists);
    }
}
