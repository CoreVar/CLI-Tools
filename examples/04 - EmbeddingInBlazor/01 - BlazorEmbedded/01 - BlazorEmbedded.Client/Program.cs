using BlazorApplication.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services
    .AddSingleton<ConsoleApplicationService>()
    .AddSingleton<GreetingService>();

await builder.Build().RunAsync();
