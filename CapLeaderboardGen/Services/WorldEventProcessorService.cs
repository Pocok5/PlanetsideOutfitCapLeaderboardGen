using CapLeaderboardGen.DataflowBlocks;
using CapLeaderboardGen.DataTypes;
using CapLeaderboardGen.DependencyInjection;
using DbgCensus.Rest.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace CapLeaderboardGen.Services
{
    internal class WorldEventProcessorService
    {
        private readonly IQueryService queryService;
        private readonly CensusConfigContainer censusConfigContainer;
        private readonly ILogger<WorldEventProcessorService> logger;

        public WorldEventProcessorService(IQueryService queryService, CensusConfigContainer censusConfigContainer, ILogger<WorldEventProcessorService> logger)
        {
            this.queryService = queryService;
            this.censusConfigContainer = censusConfigContainer;
            this.logger = logger;
        }

        public async Task ProcessEvents(DateTimeOffset startDate, DateTimeOffset endDate, int worldId, long outfitId, Action<WorldEventStreamer.ProgressUpdateEventArgs> streamProgressDelegate)
        {
            var eventStreamerBlock = new WorldEventStreamer(startDate, endDate, worldId, queryService, censusConfigContainer.GetQueryOptions(), logger);
            eventStreamerBlock.OnStreamProgressUpdated += streamProgressDelegate;

            var filterBlock = new TransformBlock<WorldFacilityEvent[], WorldFacilityEvent[]>(
                    (events) => events.Where(e => (e.OutfitId == outfitId)&&(e.FactionOld != e.FactionNew)).ToArray()
                );

            var flattenBlock = new TransformManyBlock<WorldFacilityEvent[], WorldFacilityEvent>(events => events);


            eventStreamerBlock.LinkTo(filterBlock);
            filterBlock.LinkTo(flattenBlock);

            await eventStreamerBlock.StartStreaming();
            eventStreamerBlock.OnStreamProgressUpdated -= streamProgressDelegate;
        }
    }
}
