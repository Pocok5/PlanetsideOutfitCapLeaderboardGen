using CapLeaderboardGen.DependencyInjection;
using CapLeaderboardGen.Services;
using DbgCensus.Rest;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapLeaderboardGen
{
    internal class DefaultCommand : AsyncCommand<DefaultCommand.Settings>
    {
        private readonly CensusConfigContainer censusConfigContainer;
        private readonly OutfitInfoService membersService;

        public DefaultCommand(CensusConfigContainer censusConfigContainer, OutfitInfoService membersService)
        {
            this.censusConfigContainer = censusConfigContainer;
            this.membersService = membersService;
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
            

            return 1;
        }

        public class Settings : CommandSettings
        {
            [CommandArgument(0, "<service_id>")]
            public string ServiceId { get; init; }

            [CommandArgument(1, "<outfit_tag>")]
            public string OutfitTag { get; init; }

            [CommandArgument(2, "<start_date>")]
            public DateTimeOffset StartDate { get; init; }

            [CommandArgument(3, "[end_date]")]
            public DateTimeOffset? EndDate { get; init; }

        }
    }
}
