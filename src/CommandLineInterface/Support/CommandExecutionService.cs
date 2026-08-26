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
    private readonly List<string> _replHistory = [];

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

                if (currentLine == "!!")
                {
                    if (_replHistory.Count == 0)
                    {
                        await consoleControl.WriteErrorLine("No commands in history.");
                        continue;
                    }
                    currentLine = _replHistory[^1];
                    await consoleControl.WriteLine(currentLine);
                }

                if (options.HistoryCommandName is not null && options.CommandComparer.Equals(currentLine, options.HistoryCommandName))
                {
                    for (var index = 0; index < _replHistory.Count; index++)
                        await consoleControl.WriteLine($"{index + 1,4}  {_replHistory[index]}");
                    continue;
                }

                _replHistory.Add(currentLine);
                if (_replHistory.Count > Math.Max(1, options.ReplHistoryLimit))
                    _replHistory.RemoveAt(0);

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

        if (TryFindUnknown(commandTreeContext, treeElementContext, out var unknown, out var candidates))
        {
            commandExecutionContext.Result = options.ValidationErrorExitCode;
            var suggestion = options.EnableSuggestions ? FindClosest(unknown, candidates) : null;
            await consoleControl.WriteErrorLine(suggestion is null
                ? $"Unknown command or option '{unknown}'."
                : $"Unknown command or option '{unknown}'. Did you mean '{suggestion}'?");
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
                var interruptSource = consoleControl as IConsoleInterruptSource;
                interruptSource?.ResetInterrupt();
                using var linkedCancellation = interruptSource is null ? null :
                    CancellationTokenSource.CreateLinkedTokenSource(
                        applicationLifetime.ApplicationStopping, interruptSource.InterruptToken);
                ((ICommandExecutionContextInternals)commandExecutionContext).CancellationToken =
                    linkedCancellation?.Token ?? applicationLifetime.ApplicationStopping;

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

    private static bool TryFindUnknown(CommandTreeContext context, CommandTreeElementContext executing,
        out string unknown, out IEnumerable<string> candidates)
    {
        unknown = string.Empty;
        candidates = [];
        if (context.Arguments.Length == 0) return false;

        if (context.Root.Child is null && context.Root.Element.Children.Count > 0 &&
            context.Root.Element.Arguments?.Count is not > 0 && !context.Arguments[0].StartsWith('-'))
        {
            unknown = context.Arguments[0];
            candidates = context.Root.Element.Children.Keys;
            return true;
        }

        var consumed = new HashSet<int>();
        if (executing.Options is not null)
            foreach (var option in executing.Options.Values)
                foreach (var position in option.Positions)
                {
                    consumed.Add(position);
                    if (option.Option.AcceptsValue) consumed.Add(position + 1);
                }
        if (executing.Arguments is not null)
            foreach (var argument in executing.Arguments.Values)
                foreach (var position in argument.Positions) consumed.Add(position);

        for (var index = executing.Position + 1; index < context.Arguments.Length; index++)
            if (!consumed.Contains(index) && context.Arguments[index] != "--")
            {
                unknown = context.Arguments[index];
                candidates = (executing.Element.Options?.Keys.AsEnumerable() ?? Enumerable.Empty<string>())
                    .Concat(executing.Element.OptionAliases?.Keys.AsEnumerable() ?? Enumerable.Empty<string>());
                return true;
            }
        return false;
    }

    private static string? FindClosest(string value, IEnumerable<string> candidates)
    {
        var best = candidates.Select(candidate => (candidate, distance: EditDistance(value, candidate)))
            .OrderBy(item => item.distance).FirstOrDefault();
        return best.candidate is not null && best.distance <= Math.Max(2, value.Length / 3) ? best.candidate : null;
    }

    private static int EditDistance(string left, string right)
    {
        var row = Enumerable.Range(0, right.Length + 1).ToArray();
        for (var i = 1; i <= left.Length; i++)
        {
            var diagonal = row[0]; row[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var above = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (left[i - 1] == right[j - 1] ? 0 : 1));
                diagonal = above;
            }
        }
        return row[^1];
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
