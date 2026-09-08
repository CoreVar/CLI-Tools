using System.Text.Json;

Console.WriteLine(JsonSerializer.Serialize(new
{
    message = "Hello from an independent native module",
    processId = Environment.ProcessId,
    protocol = Environment.GetEnvironmentVariable("COREVAR_MODULE_PROTOCOL"),
    version = Environment.GetEnvironmentVariable("COREVAR_MODULE_VERSION"),
    arguments = args
}));
