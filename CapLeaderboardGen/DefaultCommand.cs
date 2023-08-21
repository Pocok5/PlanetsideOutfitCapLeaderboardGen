using CapLeaderboardGen.DependencyInjection;
using CapLeaderboardGen.Services;
using DbgCensus.Rest;
using Spectre.Console;
using Spectre.Console.Cli;

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
            await progress.StartAsync(async (ctx) =>
            {
                var streamTask = ctx.AddTask("Downloading capture events");

                await worldEventProcessor.ProcessEvents(
                    settings.StartDate,
                    settings.EndDate ?? DateTimeOffset.UtcNow,
                    13,
                    membersService.OutfitInfo?.OutfitId ?? throw new InvalidOperationException("Outfit info not initialized before trying to read it."),
                    (args) => streamTask.Value = args.ProgressPercentage
                );
            });

            return 1;
        }

        public class Settings : CommandSettings
        {
            [CommandArgument(0, "<service_id>")]
            public required string ServiceId { get; init; }

            [CommandArgument(1, "<outfit_tag>")]
            public required string OutfitTag { get; init; }

            [CommandArgument(2, "<start_date>")]
            public DateTimeOffset StartDate { get; init; }

            [CommandArgument(3, "[end_date]")]
            public DateTimeOffset? EndDate { get; init; }
        }
    }
}
