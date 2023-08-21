using CapLeaderboardGen.DataTypes;
using CapLeaderboardGen.DependencyInjection;
using CapLeaderboardGen.Services;
using DbgCensus.Rest;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CapLeaderboardGen
{
    internal class DefaultCommand : AsyncCommand<DefaultCommand.Settings>
    {
        private readonly CensusConfigContainer censusConfigContainer;
        private readonly OutfitInfoService membersService;
        private readonly WorldEventProcessorService worldEventProcessor;

        public DefaultCommand(CensusConfigContainer censusConfigContainer, OutfitInfoService membersService, WorldEventProcessorService worldEventProcessor)
        {
            this.censusConfigContainer = censusConfigContainer;
            this.membersService = membersService;
            this.worldEventProcessor = worldEventProcessor;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {

            TextWriter outputWriter;
            if (settings.OutputPath is null)
            {
                outputWriter = Console.Out;
            }
            else
            {
                if (settings.OutputPath.Exists)
                {
                    if (AnsiConsole.Confirm($"[red]The path '{settings.OutputPath}' already exists. Overwrite?[/]"))
                    {
                        outputWriter = new StreamWriter(settings.OutputPath.Create());
                    }
                    else
                    {
                        return 1;
                    }
                }
                else
                {
                    outputWriter = new StreamWriter(settings.OutputPath.Create());
                }
            }


            var queryOptions = new CensusQueryOptions()
            {
                Limit = 1000,
                Namespace = "ps2:v2",
                RootEndpoint = "https://census.daybreakgames.com/",
                ServiceId = settings.ServiceId
            };
            censusConfigContainer.SetQueryOptions(queryOptions);

            var spinner = AnsiConsole.Status();
            spinner.Spinner = Spinner.Known.BouncingBar;
            await spinner.StartAsync(
                    "Retrieving outfit members",
                    async _ => await membersService.RetrieveOutfitData(settings.OutfitTag)
                );

            var progress = AnsiConsole.Progress();
            progress.RefreshRate = TimeSpan.FromMilliseconds(50);
            progress.Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn()
                        .RemainingStyle(new Style(Color.White))
                        .CompletedStyle(new Style(Color.Gold3_1)),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
                    new SpinnerColumn()
                );
            var results = await progress.StartAsync<List<OutputRecord>>(async (ctx) =>
            {
                bool postProcessingBegan = false;
                var streamTask = ctx.AddTask("Downloading capture events");

                return await worldEventProcessor.ProcessEvents(
                    settings.StartDate,
                    settings.EndDate ?? DateTimeOffset.UtcNow,
                    13,
                    membersService.OutfitInfo?.OutfitId ?? throw new InvalidOperationException("Outfit info not initialized before trying to read it."),
                    (args) => {
                        streamTask.Value = args.DownloadProgressPercentage;
                        if (args.PostProcessingInProgress && !postProcessingBegan)
                        {
                            postProcessingBegan = true;
                            var postprocTask = ctx.AddTask("Processing capture events");
                            postprocTask.IsIndeterminate = true;
                        }
                    }
                );
            });

            

            var outputJson = JsonSerializer.Serialize(results);

            outputWriter.Write(outputJson);
            outputWriter.Dispose();

            return 0;
        }

        public class Settings : CommandSettings
        {
            [CommandArgument(0, "<service_id>")]
            [Description("Your service ID without the s: prefix")]
            public required string ServiceId { get; init; }

            [CommandArgument(1, "<outfit_tag>")]
            [Description("The tag of the outfit whose captures you want to search for")]
            public required string OutfitTag { get; init; }

            [CommandArgument(2, "<start_date>")]
            [Description("The start of the search period. Accepts ISO 8601 format date/time strings.")]
            public DateTimeOffset StartDate { get; init; }

            [CommandArgument(3, "[end_date]")]
            [Description("The end of the search period. Accepts ISO 8601 format date/time strings. If omitted, the current time is used.")]
            public DateTimeOffset? EndDate { get; init; }
            [CommandOption("-o|--output")]
            [Description("The JSON output path. You must specify this if you do not redirect standard output.")]
            public FileInfo? OutputPath { get; init; }

            public override ValidationResult Validate()
            {
                if (OutputPath == null && !Console.IsOutputRedirected)
                {
                    return ValidationResult.Error("If you do not redirect the standard output, you must specify an output file with the -o|--output switch.");
                }

                return base.Validate();
            }
        }
    }
}
