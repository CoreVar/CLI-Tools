using CoreVar.CommandLineInterface.Builders;
using Microsoft.Extensions.DependencyInjection;

namespace CoreVar.CommandLineInterface;

internal sealed class ExecutionFileSystem(CommandExecutionContext context) : ICommandFileSystem
{
    private ICommandFileSystem Inner => context.Services.GetRequiredService<ICommandFileSystem>();
    private CommandFileRequest ApplyPolicy(CommandFileRequest request) => request with
    {
        AllowInteraction = request.AllowInteraction && context.EnablePrompts &&
            context.Services.GetRequiredService<CommandLineOptions>().EnablePrompts
    };

    public async ValueTask<string> ResolveReadPathAsync(CommandFileRequest request, CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, cancellationToken);
        return await Inner.ResolveReadPathAsync(ApplyPolicy(request), linked.Token);
    }
    public async ValueTask<Stream> OpenReadAsync(CommandFileRequest request, CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, cancellationToken);
        return await Inner.OpenReadAsync(ApplyPolicy(request), linked.Token);
    }
    public async ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, cancellationToken);
        return await Inner.ExistsAsync(path, linked.Token);
    }
    public async ValueTask WriteAllBytesAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, cancellationToken);
        await Inner.WriteAllBytesAsync(path, content, linked.Token);
    }
    public async ValueTask<bool> DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, cancellationToken);
        return await Inner.DeleteAsync(path, linked.Token);
    }
}
