using CapLeaderboardGen.DataTypes;
using CapLeaderboardGen.Services;
using DbgCensus.Rest;
using DbgCensus.Rest.Abstractions;
using DbgCensus.Rest.Abstractions.Queries;
using Microsoft.Extensions.Logging;
using Polly;
using System.Threading.Tasks.Dataflow;

namespace CapLeaderboardGen.DataflowBlocks
{
    internal class CharacterFacilityEventCorrelator : BufferedSourceBlockBase<PlayerFacilityEvent>, ITargetBlock<WorldFacilityEvent>
    {
        private readonly ITargetBlock<WorldFacilityEvent> actionBlock;
        private readonly IQueryService queryService;
        private readonly CensusQueryOptions queryOptions;
        private readonly ILogger? logger;
        private readonly OutfitInfoService outfitInfoService;
        private readonly FacilityInfoService facilityInfoService;
        private readonly IAsyncPolicy retryPolicy;

        public CharacterFacilityEventCorrelator(IQueryService queryService, CensusQueryOptions queryOptions, ILogger? logger, OutfitInfoService outfitInfoService, FacilityInfoService facilityInfoService, ExecutionDataflowBlockOptions? dataflowBlockOptions = null)
        {
            if (dataflowBlockOptions == null)
            {
                actionBlock = new ActionBlock<WorldFacilityEvent>(ProcessWorldEvent);
            }
            else
            {
                actionBlock = new ActionBlock<WorldFacilityEvent>(ProcessWorldEvent, dataflowBlockOptions);
            }
            
            this.queryService = queryService;
            this.queryOptions = queryOptions;
            this.logger = logger;
            this.outfitInfoService = outfitInfoService;
            this.facilityInfoService = facilityInfoService;
            retryPolicy = Policy.Handle<HttpRequestException>()
                                .Or<TaskCanceledException>()
                                .WaitAndRetryAsync(6, (cnt) => TimeSpan.FromSeconds(Math.Pow(2, cnt)), (ex, time) =>
                                {
                                    logger?.LogError(ex, "HTTP exception occured");
                                    logger?.LogError("Failed to query player facility events, will retry in {Time}", time);
                                });
        }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, WorldFacilityEvent messageValue, ISourceBlock<WorldFacilityEvent>? source, bool consumeToAccept)
        {
            return actionBlock.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }

        private async Task ProcessWorldEvent(WorldFacilityEvent facilityEvent)
        {
            var query = queryService.CreateQuery(queryOptions)
                .OnCollection("event")
                .Where("type", SearchModifier.Equals, "PlayerFacility")
                .Where("after", SearchModifier.Equals, facilityEvent.Timestamp - 1)
                .Where("before", SearchModifier.Equals, facilityEvent.Timestamp + 1)
                .WithLimit(1000);

            var result = await retryPolicy.ExecuteAsync(async () => await queryService.GetAsync<PlayerFacilityEvent[]>(query));

            var filteredResult = result?
                .Where(e =>
                        e.Timestamp == facilityEvent.Timestamp
                     && e.WorldId == facilityEvent.WorldId
                     && e.ZoneId == facilityEvent.ZoneId
                     && e.FacilityId == facilityEvent.FacilityId
                ).Where(e => outfitInfoService.GetCharacterName(e.CharacterId) is not null)
                .ToArray();

            if (filteredResult is null || filteredResult.Length == 0)
            {
                var facilityInfo = await facilityInfoService.GetFacilityInfo(facilityEvent.FacilityId);
                logger?.LogWarning("Found a facility capture with no associated player events on {Date} for {FacilityName}. " +
                    "This can occur due to all the players leaving the base before the capture timer hits zero (or API weirdness).",
                    DateTimeOffset.FromUnixTimeSeconds(facilityEvent.Timestamp), facilityInfo.FacilityName
                );
                return; //Skip this event
            }

            foreach (var item in filteredResult)
            {
                PostToBuffer(item);
            }
        }

        public override void Complete()
        {
            actionBlock.Complete();
            base.Complete();
        }
    }
}
