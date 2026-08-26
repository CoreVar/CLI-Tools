using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;
using CoreVar.CommandLineInterface.Interfaces;
using CoreVar.CommandLineInterface.Runtime;
using CoreVar.CommandLineInterface.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using CoreVar.CommandLineInterface.Execution;
using CoreVar.CommandLineInterface.Generation;

namespace CoreVar.CommandLineInterface.Support;

public class CommandExecutionService(
    IServiceProvider services,
    IApplicationContext appContext,
    IConsoleControl consoleControl,
    IHelpExecutor helpExecutor,
    CommandLineOptions options,
    CommandTree commandTree,
    IHostApplicationLifetime applicationLifetime) : ICommandExecutor, IHostedService
{
    private readonly SemaphoreSlim _executionSemaphore = new(1, 1);
    private readonly ConcurrentQueue<(string[] Arguments, Func<int, ValueTask>? Callback)> _executionQueue = [];
    private Task? _executionTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _executionTask = Task.Run(ExecutionLoop, cancellationToken);
        return Task.CompletedTask;
    }

    private async ValueTask WritePrompt()
    {
        await consoleControl.Write(options.ReplPrompt ?? "> ");
    }

    private async Task ExecutionLoop()
    {
        var applicationContext = (ApplicationContext)appContext;

		if (options.InitializationPrompt is not null && (appContext.IsReplMode || !(options.ShowInitializationPromptForReplOnly == true)))
			await consoleControl.WriteLine(options.InitializationPrompt);

		while (!appContext.IsShuttingDown)
        {
            string[] arguments;
            if (appContext.IsReplMode)
            {
                await WritePrompt();
                var currentLine = await consoleControl.ReadLine();

                if (string.IsNullOrWhiteSpace(currentLine))
                    continue;

                if (options.CommandComparer.Equals(currentLine, options.ExitCommandName ?? "exit"))
                {
                    applicationContext.ShouldShutdown();
                    continue;
                }

                arguments = ArgumentUtilities.ExpandArguments(ArgumentUtilities.ParseArguments(currentLine));

            }
            else
            {
                arguments = ArgumentUtilities.ExpandArguments(appContext.ArgumentsRetriever());
            }
            await using var serviceScope = services.CreateAsyncScope();
            var context = serviceScope.ServiceProvider.GetRequiredService<CommandExecutionContext>();
            var contextInternals = (ICommandExecutionContextInternals)context;
            contextInternals.Arguments = arguments;
            CommandTreeContext commandTreeContext;
            if (appContext.IsReplMode)
            {
                var commandTreeContextState = serviceScope.ServiceProvider.GetRequiredService<CommandTreeContextState>();
                commandTreeContextState.CommandTreeContext = commandTreeContext = CommandTreeBuilder.Load(commandTree, arguments);
            }
            else
            {
                commandTreeContext = serviceScope.ServiceProvider.GetRequiredService<CommandTreeContext>();
            }

            await _executionSemaphore.WaitAsync();
            try
            {
                await ExecuteCommand(commandTreeContext, context);

                await FlushQueueAsync();
            }
            finally
            {
                _executionSemaphore.Release();
            }

            if (!appContext.IsReplMode)
            {
                Environment.ExitCode = context.Result;
                appContext.ShouldShutdown();
            }
        }
    }

    private async ValueTask ExecuteCommand(CommandTreeContext commandTreeContext, CommandExecutionContext commandExecutionContext)
    {
        if (options.EnableVersionOption && commandExecutionContext.Arguments.Length == 1 && commandExecutionContext.Arguments[0] is "--version" or "-V")
        {
            await consoleControl.WriteLine(options.Version ?? typeof(CommandExecutionService).Assembly.GetName().Version?.ToString() ?? "unknown");
            return;
        }

        if (commandExecutionContext.Arguments.Length == 2 && options.CommandComparer.Equals(commandExecutionContext.Arguments[0], "completion"))
        {
            await consoleControl.Write(ShellCompletionGenerator.Generate(commandTree, commandExecutionContext.Arguments[1]));
            return;
        }

        CommandTreeElementContext? treeElementContext = CommandTreeHelpers.GetExecutingCommand(commandTreeContext.Root);
        if (treeElementContext.HasHelpOption)
        {
            await helpExecutor.ShowHelp(commandTreeContext);
            return;
        }


        var validationResults = CommandTreeHelpers.Validate(commandTreeContext);
        foreach (var validationResult in validationResults)
        {
            commandExecutionContext.Result = options.ValidationErrorExitCode;
            await consoleControl.WriteErrorLine(validationResult.Message);
            return;
        }

        var element = treeElementContext.Element;
        if (element.ExecuteDelegate is not null)
        {
            try
            {
                ((ICommandExecutionContextInternals)commandExecutionContext).CancellationToken = applicationLifetime.ApplicationStopping;

                if (element.DeprecationMessage is not null)
                    await consoleControl.WriteErrorLine($"Warning: {element.DeprecationMessage}");

                foreach (var validator in element.Validators)
                {
                    var result = await validator(commandExecutionContext);
                    if (!result.IsValid)
                    {
                        commandExecutionContext.Result = options.ValidationErrorExitCode;
                        await consoleControl.WriteErrorLine(result.Message ?? "Command validation failed.");
                        return;
                    }
                }

                CommandExecutionDelegate pipeline = context => element.ExecuteDelegate(context);
                var middleware = GetMiddleware(commandTreeContext.Root);
                for (var index = middleware.Count - 1; index >= 0; index--)
                {
                    var current = middleware[index];
                    var next = pipeline;
                    pipeline = context => current(context, next);
                }

                await pipeline(commandExecutionContext);
            }
            catch (OperationCanceledException) when (commandExecutionContext.CancellationToken.IsCancellationRequested)
            {
                commandExecutionContext.Result = options.CancellationExitCode;
            }
            catch (Exception ex)
            {
                if (commandExecutionContext.Result == 0)
                    commandExecutionContext.Result = options.CommandErrorExitCode;

                await consoleControl.WriteErrorLine($"Error executing command '{CommandTreeHelpers.GetCommandName(services, commandTreeContext)}': {ex.Message}");
            }
        }
        else
        {
            commandExecutionContext.Result = options.ValidationErrorExitCode;
            await consoleControl.WriteErrorLine($"'{CommandTreeHelpers.GetCommandName(services, commandTreeContext)}' is not a command that can be executed.");
        }
    }

    private static List<CoreVar.CommandLineInterface.Execution.CommandMiddleware> GetMiddleware(CommandTreeElementContext root)
    {
        var middleware = new List<CoreVar.CommandLineInterface.Execution.CommandMiddleware>();
        for (CommandTreeElementContext? current = root; current is not null; current = current.Child)
            middleware.AddRange(current.Element.Middleware);
        return middleware;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executionTask is not null && _executionTask.IsCompleted)
            await _executionTask.ConfigureAwait(false);
    }

    public async ValueTask<int> Execute(params string[] args)
    {
        await using var serviceScope = services.CreateAsyncScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<CommandExecutionContext>();
        var contextInternals = (ICommandExecutionContextInternals)context;
        contextInternals.Arguments = args;
        var commandTreeContextState = serviceScope.ServiceProvider.GetRequiredService<CommandTreeContextState>();
        commandTreeContextState.CommandTreeContext = CommandTreeBuilder.Load(commandTree, args);

        await _executionSemaphore.WaitAsync();
        try
        {
            await consoleControl.WriteLine(ArgumentUtilities.ConvertToArgumentsString(args));
            await ExecuteCommand(commandTreeContextState.CommandTreeContext, context);

            if (appContext.IsReplMode)
                await WritePrompt();

            await FlushQueueAsync();
        }
        finally
        {
            _executionSemaphore.Release();
        }

        return context.Result;
    }

    private async ValueTask FlushQueueAsync()
    {
        while (_executionQueue.TryDequeue(out var queuedItem))
        {
            var args = queuedItem.Arguments;
            await using var serviceScope = services.CreateAsyncScope();
            var context = serviceScope.ServiceProvider.GetRequiredService<CommandExecutionContext>();
            var contextInternals = (ICommandExecutionContextInternals)context;
            contextInternals.Arguments = args;
            var commandTreeContextState = serviceScope.ServiceProvider.GetRequiredService<CommandTreeContextState>();
            commandTreeContextState.CommandTreeContext = CommandTreeBuilder.Load(commandTree, args);

            await WritePrompt();
            await consoleControl.WriteLine(ArgumentUtilities.ConvertToArgumentsString(args));
            await ExecuteCommand(commandTreeContextState.CommandTreeContext, context);

            if (queuedItem.Callback is not null)
                await queuedItem.Callback(context.Result);
        }
    }

    public void EnqueueExecution(string[] args, Func<int, ValueTask>? callback = null)
    {
        _executionQueue.Enqueue((args, callback));
    }
}
