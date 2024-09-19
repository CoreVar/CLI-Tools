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

namespace CoreVar.CommandLineInterface.Support;

public class CommandExecutionService(
    IServiceProvider services,
    IApplicationContext appContext,
    IConsoleControl consoleControl,
    IHelpExecutor helpExecutor,
    CommandLineOptions options,
    CommandTree commandTree) : ICommandExecutor, IHostedService
{
    private readonly SemaphoreSlim _executionSemaphore = new(1, 1);
    private readonly ConcurrentQueue<(string[] Arguments, Func<int, ValueTask>? Callback)> _executionQueue = [];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Factory.StartNew(ExecutionLoop, TaskCreationOptions.LongRunning);
        return Task.CompletedTask;
    }

    private async ValueTask WritePrompt()
    {
        await consoleControl.Write(options.ReplPrompt ?? "> ");
    }

    private async void ExecutionLoop()
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

                arguments = ArgumentUtilities.ParseArguments(currentLine);

            }
            else
            {
                arguments = Environment.GetCommandLineArgs().Skip(1).ToArray();
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
        CommandTreeElementContext? treeElementContext = CommandTreeHelpers.GetExecutingCommand(commandTreeContext.Root);
        if (treeElementContext.HasHelpOption)
        {
            await helpExecutor.ShowHelp(commandTreeContext);
            return;
        }


        var validationResults = CommandTreeHelpers.Validate(commandTreeContext);
        foreach (var validationResult in validationResults)
        {
            await consoleControl.WriteErrorLine(validationResult.Message);
            return;
        }

        var element = treeElementContext.Element;
        if (element.ExecuteDelegate is not null)
        {
            try
            {
                await element.ExecuteDelegate(commandExecutionContext);
            }
            catch (Exception ex)
            {
                if (commandExecutionContext.Result == 0)
                    commandExecutionContext.Result = -1;

                await consoleControl.WriteErrorLine($"Error executing command '{CommandTreeHelpers.GetCommandName(services, commandTreeContext)}': {ex.Message}");
            }
        }
        else
            await consoleControl.WriteErrorLine($"'{CommandTreeHelpers.GetCommandName(services, commandTreeContext)}' is not a command that can be executed.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

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