using CoreVar.CommandLineInterface.Builders;

namespace CoreVar.CommandLineInterface.Modules;

public static class ExternalModuleExtensions
{
    /// <summary>Projects all locally installed external modules into the command tree.</summary>
    public static ICommandLineBuilder ExternalModules(this ICommandLineBuilder builder, string? root = null)
        => builder.ExternalModules(context => new ModuleInvocationContext(context.Services, context.Console), root);

    /// <summary>Projects installed modules using an isolated context resolved for every invocation.</summary>
    public static ICommandLineBuilder ExternalModules(this ICommandLineBuilder builder,
        Func<CommandExecutionContext, ModuleInvocationContext> invocationFactory, string? root = null)
    {
        var paths = new ModulePaths(root);
        var store = new ModuleStore(paths);
        var runner = new ModuleRunner();
        foreach (var id in store.InstalledModuleIds().OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            var manifest = store.GetCurrentAsync(id).AsTask().GetAwaiter().GetResult();
            if (manifest is null) continue;
            foreach (var command in manifest.Commands) Project(builder, command, manifest, runner, invocationFactory);
        }
        return builder;
    }

    /// <summary>Adds module install, update, list, rollback, and remove commands.</summary>
    public static ICommandLineBuilder ModuleManagement(this ICommandLineBuilder builder, string? root = null)
    {
        var paths = new ModulePaths(root);
        var store = new ModuleStore(paths);
        var installer = new ModuleInstaller(paths, store, new ModuleRuntimeProvisioner());
        var manager = new ModuleManager(new ModuleCatalogClient(), installer, store);
        return builder.Command("module", module => module.Description("Installs and updates language-neutral CLI modules.")
            .Command("list", command => command.OnExecute(new Func<CommandExecutionContext, ValueTask>(async context =>
            {
                foreach (var id in store.InstalledModuleIds().OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                {
                    var manifest = await store.GetCurrentAsync(id, context.CancellationToken);
                    if (manifest is not null) await context.Console.WriteLine($"{manifest.Id} {manifest.Version}");
                }
            })))
            .Command("install", command =>
            {
                var id = command.Argument<string>("id");
                var catalog = command.Option<Uri>("--catalog").IsRequired();
                var channel = command.Option<string>("--channel").Default("stable");
                var version = command.Option<string?>("--version");
                command.OnExecute(new Func<CommandExecutionContext, ValueTask>(async context =>
                {
                    var installed = await manager.InstallAsync(context.GetOption(catalog), context.GetArgument(id), context.GetOption(channel), context.GetOption(version), context.CancellationToken);
                    await context.Console.WriteLine($"Installed {installed.Id} {installed.Version}.");
                }));
            })
            .Command("update", command =>
            {
                var id = command.Argument<string>("id");
                command.OnExecute(new Func<CommandExecutionContext, ValueTask>(async context =>
                {
                    var updated = await manager.UpdateAsync(context.GetArgument(id), context.CancellationToken);
                    await context.Console.WriteLine(updated is null ? "Already up to date." : $"Updated {updated.Id} to {updated.Version}.");
                }));
            })
            .Command("rollback", command =>
            {
                var id = command.Argument<string>("id");
                command.OnExecute(new Func<CommandExecutionContext, ValueTask>(async context =>
                    await context.Console.WriteLine(await manager.RollbackAsync(context.GetArgument(id), context.CancellationToken) ? "Rolled back." : "No previous version is available.")));
            })
            .Command("remove", command =>
            {
                var id = command.Argument<string>("id");
                command.OnExecute(context =>
                {
                    manager.Remove(context.GetArgument(id));
                    return context.Console.WriteLine("Module removed.");
                });
            }));
    }

    private static void Project(IParentBuilder parent, ModuleCommand definition, ModuleManifest manifest, ModuleRunner runner,
        Func<CommandExecutionContext, ModuleInvocationContext> invocationFactory)
    {
        parent.Command(definition.Name, command =>
        {
            if (definition.Description is not null) command.Description(definition.Description);
            if (definition.Hidden) command.Hidden();
            if (definition.Aliases.Count > 0) command.Alias([.. definition.Aliases]);
            foreach (var option in definition.Options)
            {
                if (option.RequiresValue)
                {
                    var projected = command.Option<string?>(option.Name);
                    if (option.Description is not null) projected.Description(option.Description);
                    if (option.Aliases.Count > 0) projected.WithAlias([.. option.Aliases]);
                    if (option.Completions.Count > 0) projected.Complete([.. option.Completions]);
                }
                else
                {
                    var projected = command.Option(option.Name);
                    if (option.Description is not null) projected.Description(option.Description);
                    if (option.Aliases.Count > 0) projected.WithAlias([.. option.Aliases]);
                }
            }
            foreach (var argument in definition.Arguments)
            {
                var projected = command.Argument<string>(argument.Name);
                if (!argument.Required) projected.IsOptional();
                if (argument.Remaining) projected.Variadic();
                if (argument.Description is not null) projected.Description(argument.Description);
                if (argument.Completions.Count > 0) projected.Complete([.. argument.Completions]);
            }
            foreach (var child in definition.Commands) Project(command, child, manifest, runner, invocationFactory);
            if (definition.Commands.Count == 0)
                command.OnExecute(new Func<CommandExecutionContext, ValueTask<int>>(context => runner.RunAsync(manifest, context.Arguments, invocationFactory(context), context.CancellationToken)));
        });
    }
}
