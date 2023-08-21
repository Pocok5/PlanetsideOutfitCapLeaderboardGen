using CapLeaderboardGen.DataflowBlocks;
using CapLeaderboardGen.DependencyInjection;
using DbgCensus.Rest.Abstractions;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks.Dataflow;

namespace CapLeaderboardGen.Services
{
    internal class DataflowBlockFactory
    {
        private readonly OutfitInfoService outfitInfoService;
        private readonly IQueryService queryService;
        private readonly CensusConfigContainer censusConfigContainer;
        private readonly FacilityInfoService facilityInfoService;
        private readonly ILoggerFactory loggerFactory;

        public DataflowBlockFactory(OutfitInfoService outfitInfoService, IQueryService queryService, CensusConfigContainer censusConfigContainer, FacilityInfoService facilityInfoService, ILoggerFactory loggerFactory)
        {
            this.outfitInfoService = outfitInfoService;
            this.queryService = queryService;
            this.censusConfigContainer = censusConfigContainer;
            this.facilityInfoService = facilityInfoService;
            this.loggerFactory = loggerFactory;
        }

        public CharacterFacilityEventCorrelator CreateCharFacilityCorrelatorBlock() =>
          new CharacterFacilityEventCorrelator(
            queryService,
            censusConfigContainer.GetQueryOptions(),
            loggerFactory.CreateLogger<CharacterFacilityEventCorrelator>(),
            outfitInfoService,
            facilityInfoService, null);

        public CharacterFacilityEventCorrelator CreateCharFacilityCorrelatorBlock(ExecutionDataflowBlockOptions blockOptions) =>
          new CharacterFacilityEventCorrelator(
            queryService,
            censusConfigContainer.GetQueryOptions(),
            loggerFactory.CreateLogger<CharacterFacilityEventCorrelator>(),
            outfitInfoService,
            facilityInfoService, blockOptions);

        public WorldEventStreamer CreateWorldEventStreamerBlock(int worldId, DateTimeOffset startDate, DateTimeOffset endDate) =>
            new WorldEventStreamer(
                startDate,
                endDate,
                worldId,
                queryService,
                censusConfigContainer.GetQueryOptions(),
                loggerFactory.CreateLogger<WorldEventStreamer>()
            );
    }
}
