using System.Text;
using CoreVar.CommandLineInterface;
using CoreVar.CommandLineInterface.Support;
using CoreVar.CommandLineInterface.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CommandLineInterface.Tests;

[Collection("Prompt host tests")]
public sealed class CommandFileTests
{
    [Fact]
    public async Task Missing_read_waits_for_supply_then_reuses_cache()
    {
        using var files = new BlazorFileSystem();
        using var picker = files.AttachPicker();
        var read = files.OpenReadAsync(new("config.json")).AsTask();
        Assert.False(read.IsCompleted);
        var request = Assert.IsType<BrowserFileRequest>(files.PendingRequest);
        Assert.True(await files.SupplyAsync(request.Id, Bytes("hello")));
        using var stream = await read;
        Assert.Equal("hello", await new StreamReader(stream).ReadToEndAsync());
        Assert.Null(files.PendingRequest);
        using var cached = await files.OpenReadAsync(new("config.json") { AllowInteraction = false });
        Assert.Equal("hello", await new StreamReader(cached).ReadToEndAsync());
        Assert.False(cached.CanWrite);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unavailable_picker_or_batch_fails_without_waiting(bool attach)
    {
        using var files = new BlazorFileSystem();
        using var picker = attach ? files.AttachPicker() : null;
        await Assert.ThrowsAsync<FileNotFoundException>(() => files.OpenReadAsync(new("missing") { AllowInteraction = !attach }).AsTask());
        Assert.Null(files.PendingRequest);
    }

    [Fact]
    public async Task Cancel_and_detach_unblock_without_caching_or_accepting_stale_upload()
    {
        using var files = new BlazorFileSystem();
        var picker = files.AttachPicker();
        var read = files.OpenReadAsync(new("cancel")).AsTask();
        var id = files.PendingRequest!.Id;
        files.CancelRequest(id);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
        Assert.False(await files.SupplyAsync(id, Bytes("late")));
        Assert.False(await files.ExistsAsync("cancel"));
        var next = files.OpenReadAsync(new("detach")).AsTask();
        picker.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => next);
        Assert.Null(files.PendingRequest);
    }

    [Fact]
    public async Task Command_cancellation_and_disposal_cancel_pending_reads()
    {
        using var files = new BlazorFileSystem();
        using var picker = files.AttachPicker();
        using var cancel = new CancellationTokenSource();
        var read = files.OpenReadAsync(new("cancel"), cancel.Token).AsTask();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
        var next = files.OpenReadAsync(new("dispose")).AsTask();
        files.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => next);
    }

    [Fact]
    public async Task Upload_limits_and_failed_reads_never_commit_partial_content()
    {
        using var files = new BlazorFileSystem(new() { MaxFileBytes = 4, MaxCacheBytes = 6 });
        using var picker = files.AttachPicker();
        var read = files.OpenReadAsync(new("small")).AsTask();
        var id = files.PendingRequest!.Id;
        await Assert.ThrowsAsync<IOException>(() => files.SupplyAsync(id, Bytes("12345")).AsTask());
        Assert.False(read.IsCompleted);
        Assert.False(await files.ExistsAsync("small"));
        await Assert.ThrowsAsync<IOException>(() => files.SupplyAsync(id, new FailingStream()).AsTask());
        Assert.False(await files.ExistsAsync("small"));
        Assert.True(await files.SupplyAsync(id, Bytes("1234")));
        (await read).Dispose();
        await Assert.ThrowsAsync<IOException>(() => files.WriteAllBytesAsync("overflow", Encoding.UTF8.GetBytes("abc")).AsTask());
        Assert.False(await files.ExistsAsync("overflow"));
        Assert.True(await files.DeleteAsync("small"));
        await files.WriteAllBytesAsync("overflow", Encoding.UTF8.GetBytes("abc"));
    }

    [Fact]
    public async Task Concurrent_same_file_requests_share_uploaded_content()
    {
        using var files = new BlazorFileSystem();
        using var picker = files.AttachPicker();
        var first = files.OpenReadAsync(new("shared")).AsTask();
        var second = files.OpenReadAsync(new("shared")).AsTask();
        await files.SupplyAsync(files.PendingRequest!.Id, Bytes("content"));
        (await first).Dispose();
        (await second).Dispose();
        Assert.Null(files.PendingRequest);
        Assert.Single(files.CachedPaths);
    }

    [Fact]
    public async Task Virtual_cache_is_isolated_and_cannot_traverse_to_server_files()
    {
        using var first = new BlazorFileSystem();
        using var second = new BlazorFileSystem();
        await first.WriteAllBytesAsync("folder/file", Encoding.UTF8.GetBytes("data"));
        Assert.True(await first.ExistsAsync("folder\\file"));
        Assert.False(await second.ExistsAsync("folder/file"));
        foreach (var invalid in new[] { "../secret", "a/../secret", "C:\\secret", "" })
            await Assert.ThrowsAnyAsync<ArgumentException>(() => first.OpenReadAsync(new(invalid)).AsTask());
    }

    [Fact]
    public async Task Native_files_use_the_same_read_write_delete_contract()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        ICommandFileSystem files = new NativeCommandFileSystem(new NativeCommandPromptService(new TestConsoleControl()));
        try
        {
            await files.WriteAllBytesAsync(path, Encoding.UTF8.GetBytes("native"));
            Assert.True(await files.ExistsAsync(path));
            using var stream = await files.OpenReadAsync(path);
            Assert.Equal("native", await new StreamReader(stream).ReadToEndAsync());
        }
        finally { await files.DeleteAsync(path); }
        Assert.False(await files.ExistsAsync(path));
        await Assert.ThrowsAsync<CommandPromptException>(() => files.ResolveReadPathAsync(new()).AsTask());
    }

    [Fact]
    public async Task Filename_metadata_waits_before_handler_and_keeps_argv_unchanged()
    {
        using var files = new BlazorFileSystem();
        using var picker = files.AttachPicker();
        var builder = CommandLineBuilder.Create("files");
        builder.Components<FileTestContext>();
        builder.SetupHostBuilder(host => host.Services.AddSingleton<ICommandFileSystem>(files));
        var result = builder.TestAsync("read-file").AsTask();
        await WaitForRequest(files);
        Assert.False(result.IsCompleted);
        Assert.Equal("Configuration", files.PendingRequest!.Label);
        await files.SupplyAsync(files.PendingRequest.Id, Bytes("fixture"));
        Assert.Equal(0, (await result).ExitCode);
        Assert.Equal(0, (await builder.TestAsync("read-file")).ExitCode);
        Assert.Null(files.PendingRequest);
    }

    [Fact]
    public async Task Batch_blocks_dynamic_missing_file_but_can_read_existing_cache()
    {
        using var files = new BlazorFileSystem();
        using var picker = files.AttachPicker();
        var builder = CommandLineBuilder.Create("files");
        builder.SetupHostBuilder(host => host.Services.AddSingleton<ICommandFileSystem>(files));
        builder.OnExecute(async context => { using var stream = await context.Files.OpenReadAsync("runtime"); });
        await using var app = builder.Build(() => []);
        var executor = app.Host.Services.GetRequiredService<ICommandExecutor>();
        Assert.NotEqual(0, await executor.Execute(new CommandExecutionOptions { EnablePrompts = false }));
        Assert.Null(files.PendingRequest);
        await files.WriteAllBytesAsync("runtime", Encoding.UTF8.GetBytes("cached"));
        Assert.Equal(0, await executor.Execute(new CommandExecutionOptions { EnablePrompts = false }));
    }

    private static MemoryStream Bytes(string text) => new(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task File_count_limit_also_bounds_empty_files()
    {
        using var files = new BlazorFileSystem(new() { MaxCachedFiles = 1 });
        await files.WriteAllBytesAsync("one", ReadOnlyMemory<byte>.Empty);
        await Assert.ThrowsAsync<IOException>(() => files.WriteAllBytesAsync("two", ReadOnlyMemory<byte>.Empty).AsTask());
        await files.WriteAllBytesAsync("one", Encoding.UTF8.GetBytes("replacement"));
        Assert.Single(files.CachedPaths);
    }

    [Fact]
    public async Task File_metadata_is_generated_for_properties_and_positional_parameters()
    {
        var builder = CommandLineBuilder.Create("files");
        builder.Components<FilePropertyContext>();
        await using var app = builder.Build(() => []);
        var tree = app.Host.Services.GetRequiredService<CoreVar.CommandLineInterface.Runtime.CommandTree>();
        var command = tree.Root.Children["file-properties"];
        Assert.True(command.Options!["--option"].InputFile);
        Assert.True(command.Arguments![0].InputFile);
        Assert.True(command.Arguments[1].InputFile);
    }
    private static async Task WaitForRequest(BlazorFileSystem files)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (files.PendingRequest is null) await Task.Delay(10, timeout.Token);
    }
    private sealed class FailingStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromException<int>(new IOException("Interrupted upload"));
    }
}

[CommandName("read-file")]
public sealed class FileTestComponent : CommandLineComponent
{
    public async Task ExecuteAsync([CommandOption("--config", InputFile = true, PromptLabel = "Configuration")] string path)
    {
        using var stream = await Context.Files.OpenReadAsync(path);
        if (await new StreamReader(stream).ReadToEndAsync() != "fixture") throw new Exception("Incorrect file content");
        if (Context.Arguments.Length != 1) throw new Exception("Original argv was changed");
    }
}
[Component<FileTestComponent>]
public partial class FileTestContext : ComponentContext;

[CommandName("file-properties")]
public sealed class FilePropertyComponent : CommandLineComponent
{
    [CommandOption("--option", InputFile = true)] public string Option { get; set; } = "";
    [CommandArgument("argument", InputFile = true, Index = 0)] public string Argument { get; set; } = "";
    public Task ExecuteAsync([CommandArgument("parameter", InputFile = true, Index = 1)] string parameter) => Task.CompletedTask;
}
[Component<FilePropertyComponent>]
public partial class FilePropertyContext : ComponentContext;
