using CapLeaderboardGen.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.Spectre;
using Spectre.Console.Cli;

namespace CapLeaderboardGen;
class Program
{
    static async Task<int> Main(string[] args)
    {
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
                .WriteTo.Spectre(outputTemplate: "[{Level:u4}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            conf.AddSerilog(logger);
        });

        return sc;
    }
}
