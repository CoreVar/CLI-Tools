using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface;

public static class BlazorHostApplicationBuilderExtensions
{

    public static IHostApplicationBuilder AddBlazorConsoleControl(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddSingleton<BlazorConsoleControl>()
            .AddSingleton<IConsoleControl>(sp => sp.GetRequiredService<BlazorConsoleControl>());

        return builder;
    }

}
