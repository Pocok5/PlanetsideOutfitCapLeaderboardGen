using CapLeaderboardGen.DataTypes;
using CapLeaderboardGen.DependencyInjection;
using CapLeaderboardGen.Services;
using DbgCensus.Rest;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text.Json;

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
            if (settings.OutputPath?.Exists == true && !AnsiConsole.Confirm($"[red]The path '{settings.OutputPath}' already exists. Overwrite?[/]"))
            {
                return 1;
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
                        .RemainingStyle(new Style(Color.Silver))
                        .CompletedStyle(new Style(Color.Blue)),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
                    new SpinnerColumn()
                );
            var results = await progress.StartAsync(async (ctx) =>
            {
                bool postProcessingBegan = false;
                var streamTask = ctx.AddTask("Downloading capture events");

                return await worldEventProcessor.ProcessEvents(
                    settings.StartDate,
                    settings.EndDate ?? DateTimeOffset.UtcNow,
                    13,
                    membersService.OutfitInfo?.OutfitId ?? throw new InvalidOperationException("Outfit info not initialized before trying to read it."),
                    (args) =>
                    {
                        streamTask.Value = args.DownloadProgressPercentage;
                        if (args.PostProcessingInProgress && !postProcessingBegan)
                        {
                            postProcessingBegan = true;
                            streamTask.StopTask();
                            var postprocTask = ctx.AddTask("Processing capture events");
                            postprocTask.IsIndeterminate = true;
                        }
                    }
                );
            });

            TextWriter outputWriter;
            if (settings.OutputPath is not null)
            {
                outputWriter = new StreamWriter(settings.OutputPath.Create());
            }
            else
            {
                outputWriter = Console.Out;
            }
            var outputJson = JsonSerializer.Serialize(results);

            outputWriter.Write(outputJson);
            outputWriter.Dispose();

            if (settings.Table)
            {
                RenderTable(results);
            }

            return 0;
        }

        private void RenderTable(IEnumerable<OutputRecord> outputRecords)
        {
            var table = new Table();
            table.AddColumns(
                    new TableColumn("Name"),
                    new TableColumn("Captures").RightAligned(),
                    new TableColumn("CharacterId").Centered()
                );

            var query = outputRecords
                    .GroupBy(x => new
                    {
                        x.CharacterId,
                        x.CharacterName
                    })
                    .Select(x => new
                    {
                        x.Key.CharacterId,
                        x.Key.CharacterName,
                        Captures = x.Count()
                    }).OrderByDescending(x=>x.Captures);

            foreach (var elem in query)
            {
                table.AddRow(elem.CharacterName, elem.Captures.ToString(), elem.CharacterId.ToString());
            }

            AnsiConsole.Clear();
            AnsiConsole.Write(table);
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

            [CommandOption("-t|--table")]
            [Description("Display a table of the aggregated base captures per person")]
            public bool Table { get; init; }

            public override ValidationResult Validate()
            {
                if (OutputPath == null && !Console.IsOutputRedirected && !Table)
                {
                    return ValidationResult.Error("If you do not redirect the standard output, you must specify an output file with the -o|--output switch or display a results table with -t|--table.");
                }

                return base.Validate();
            }
        }
    }
}
