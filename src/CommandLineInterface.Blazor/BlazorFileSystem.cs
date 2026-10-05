namespace CoreVar.CommandLineInterface;

public sealed class BlazorFileSystemOptions
{
    public int MaxFileBytes { get; init; } = 10 * 1024 * 1024;
    public long MaxCacheBytes { get; init; } = 50 * 1024 * 1024;
    public int MaxCachedFiles { get; init; } = 100;
}

public sealed record BrowserFileRequest(Guid Id, string Path, string Label, long MaxFileBytes, bool IsInferred = false);

/// <summary>One terminal's bounded in-memory filesystem. Never reads or writes host disk paths.</summary>
public sealed class BlazorFileSystem : ICommandFileSystem, IDisposable
{
    private readonly object sync = new();
    private readonly Dictionary<string, byte[]> cache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim requests = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly BlazorFileSystemOptions options;
    private Pending? pending;
    private long cacheBytes;
    private int pickers;
    private bool disposed;

    public BlazorFileSystem(BlazorFileSystemOptions? options = null)
    {
        this.options = options ?? new();
        if (this.options.MaxFileBytes <= 0 || this.options.MaxCacheBytes < this.options.MaxFileBytes || this.options.MaxCachedFiles <= 0)
            throw new ArgumentOutOfRangeException(nameof(options));
    }

    public event EventHandler? Changed;
    public BrowserFileRequest? PendingRequest { get { lock (sync) return pending?.Request; } }
    public IReadOnlyList<string> CachedPaths { get { lock (sync) return cache.Keys.Order(StringComparer.Ordinal).ToArray(); } }

    /// <summary>Registers a visible picker. Removing the last picker cancels an outstanding request.</summary>
    public IDisposable AttachPicker()
    {
        lock (sync) { ObjectDisposedException.ThrowIf(disposed, this); pickers++; }
        return new PickerLease(this);
    }

    public async ValueTask<string> ResolveReadPathAsync(CommandFileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var path = Normalize(request.Path ?? request.SuggestedPath ?? "inputs/file");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        await requests.WaitAsync(linked.Token);
        Pending? current = null;
        try
        {
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                linked.Token.ThrowIfCancellationRequested();
                if (cache.ContainsKey(path)) return path;
                if (!request.AllowInteraction || pickers == 0)
                    throw new FileNotFoundException("Input file is not cached and interactive file selection is unavailable.", path);
                current = new Pending(new(Guid.NewGuid(), path, request.Label ?? "Supply input file", options.MaxFileBytes, request.Path is null), linked.Token);
                pending = current;
            }
            Changed?.Invoke(this, EventArgs.Empty);
            return await current.Completion.Task.WaitAsync(current.Cancellation.Token);
        }
        finally
        {
            lock (sync) { if (ReferenceEquals(pending, current)) pending = null; }
            current?.Cancellation.Dispose();
            requests.Release();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async ValueTask<Stream> OpenReadAsync(CommandFileRequest request, CancellationToken cancellationToken = default)
    {
        var path = await ResolveReadPathAsync(request, cancellationToken);
        lock (sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!cache.TryGetValue(path, out var content)) throw new FileNotFoundException("Cached file was removed.", path);
            return new MemoryStream(content, 0, content.Length, writable: false, publiclyVisible: false);
        }
    }

    /// <summary>Supplies bytes for the current request only. Failed reads never populate the cache.</summary>
    public async ValueTask<bool> SupplyAsync(Guid requestId, Stream content, CancellationToken cancellationToken = default)
    {
        Pending current;
        CancellationToken requestToken;
        lock (sync)
        {
            if (disposed || pending is null || pending.Request.Id != requestId || pending.Supplying || pending.Completion.Task.IsCompleted) return false;
            current = pending;
            requestToken = current.Cancellation.Token;
            current.Supplying = true;
        }
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, requestToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int count;
            while ((count = await content.ReadAsync(chunk, linked.Token)) != 0)
            {
                if (buffer.Length + count > options.MaxFileBytes) throw new IOException("The selected file exceeds the per-file limit.");
                await buffer.WriteAsync(chunk.AsMemory(0, count), linked.Token);
            }
            var bytes = buffer.ToArray();
            lock (sync)
            {
                linked.Token.ThrowIfCancellationRequested();
                if (disposed || !ReferenceEquals(pending, current)) return false;
                Put(current.Request.Path, bytes);
                current.Completion.TrySetResult(current.Request.Path);
            }
            return true;
        }
        finally { lock (sync) current.Supplying = false; }
    }

    public void CancelRequest(Guid requestId)
    {
        lock (sync)
            if (pending?.Request.Id == requestId) pending.Cancellation.Cancel();
    }

    public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        path = Normalize(path);
        lock (sync) { ObjectDisposedException.ThrowIf(disposed, this); return ValueTask.FromResult(cache.ContainsKey(path)); }
    }

    public ValueTask WriteAllBytesAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        path = Normalize(path);
        if (content.Length > options.MaxFileBytes) throw new IOException("The file exceeds the per-file limit.");
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            Put(path, content.ToArray());
        }
        Changed?.Invoke(this, EventArgs.Empty);
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        path = Normalize(path);
        bool removed;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            removed = cache.Remove(path, out var bytes);
            if (removed) cacheBytes -= bytes!.Length;
        }
        Changed?.Invoke(this, EventArgs.Empty);
        return ValueTask.FromResult(removed);
    }

    public void Clear()
    {
        lock (sync) { ObjectDisposedException.ThrowIf(disposed, this); cache.Clear(); cacheBytes = 0; }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Put(string path, byte[] bytes)
    {
        if (!cache.ContainsKey(path) && cache.Count >= options.MaxCachedFiles)
            throw new IOException("The terminal file cache is full. Remove cached files and retry.");
        var nextSize = cacheBytes - (cache.TryGetValue(path, out var previous) ? previous.Length : 0) + bytes.Length;
        if (nextSize > options.MaxCacheBytes) throw new IOException("The terminal file cache is full. Remove cached files and retry.");
        cache[path] = bytes;
        cacheBytes = nextSize;
    }

    private static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.Length > 512) throw new ArgumentException("Virtual file paths are limited to 512 characters.", nameof(path));
        var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(part => part is "." or ".." || part.Contains(':') || part.Any(char.IsControl)))
            throw new ArgumentException("Use a relative virtual file path without parent traversal or drive names.", nameof(path));
        return string.Join('/', parts);
    }

    private void DetachPicker()
    {
        lock (sync)
        {
            pickers--;
            if (pickers == 0) pending?.Cancellation.Cancel();
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            lifetime.Cancel();
            cache.Clear();
            cacheBytes = 0;
        }
    }

    private sealed class Pending(BrowserFileRequest request, CancellationToken token)
    {
        public BrowserFileRequest Request { get; } = request;
        public TaskCompletionSource<string> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationTokenSource Cancellation { get; } = CancellationTokenSource.CreateLinkedTokenSource(token);
        public bool Supplying { get; set; }
    }
    private sealed class PickerLease(BlazorFileSystem owner) : IDisposable
    {
        private BlazorFileSystem? current = owner;
        public void Dispose() => Interlocked.Exchange(ref current, null)?.DetachPicker();
    }
}
