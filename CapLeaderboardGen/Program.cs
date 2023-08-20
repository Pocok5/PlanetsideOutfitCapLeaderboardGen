using CapLeaderboardGen.DependencyInjection;
using CapLeaderboardGen.Services;
using DbgCensus.Rest.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.Spectre;
using Spectre.Console.Cli;
using System.Text;

namespace CapLeaderboardGen;
class Program
{
    static async Task<int> Main(string[] args)
    {
        System.Console.OutputEncoding = Encoding.UTF8;
        var typeRegistrar = new TypeRegistrar(ConfigureServices());
        var app = new CommandApp<DefaultCommand>(typeRegistrar);

        return await app.RunAsync(args);
    }

    static IServiceCollection ConfigureServices()
    {
        var sc = new ServiceCollection();
        sc.AddLogging(conf =>
        {
            conf.ClearProviders();
            var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("System.Net", Serilog.Events.LogEventLevel.Warning)
            .WriteTo.Spectre(outputTemplate: "[{Level:u4}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

            conf.AddSerilog(logger);
        });

        sc.AddCensusRestServices(maxRetryAttempts: 0);
        sc.AddScoped<OutfitInfoService>();
        sc.AddScoped<WorldEventProcessorService>();
        sc.AddSingleton<CensusConfigContainer>();
        return sc;
    }
}
