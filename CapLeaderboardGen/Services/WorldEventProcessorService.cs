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
        private readonly DataflowBlockFactory blockFactory;
        private readonly OutfitInfoService outfitInfo;
        private readonly FacilityInfoService facilityInfoService;
        private readonly ILogger<WorldEventProcessorService> logger;

        public WorldEventProcessorService(DataflowBlockFactory blockFactory, OutfitInfoService outfitInfoService, FacilityInfoService facilityInfoService, ILogger<WorldEventProcessorService> logger)
        {
            this.blockFactory = blockFactory;
            this.outfitInfo = outfitInfoService;
            this.facilityInfoService = facilityInfoService;
            this.logger = logger;
        }

        public async Task<List<OutputRecord>> ProcessEvents(DateTimeOffset startDate, DateTimeOffset endDate, int worldId, long outfitId, Action<WorldEventStreamer.ProgressUpdateEventArgs> streamProgressDelegate)
        {
            var finishedList = new List<OutputRecord>();

            var eventStreamerBlock = blockFactory.CreateWorldEventStreamerBlock(worldId, startDate, endDate);
            eventStreamerBlock.OnStreamProgressUpdated += streamProgressDelegate;

            var filterBlock = new TransformBlock<WorldFacilityEvent[], WorldFacilityEvent[]>(
                    (events) => events.Where(e => (e.OutfitId == outfitId) && (e.FactionOld != e.FactionNew)).ToArray()
                );

            var flattenBlock = new TransformManyBlock<WorldFacilityEvent[], WorldFacilityEvent>(events => events);

            var playerCorrelatorBlock = blockFactory.CreateCharFacilityCorrelatorBlock(new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 4 });

            var outputTransformBlock = new TransformBlock<PlayerFacilityEvent, OutputRecord>(CreateOutputRecord);

            var listAppendBlock = new ActionBlock<OutputRecord>(finishedList.Add);

            DataflowLinkOptions linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
            eventStreamerBlock.LinkTo(filterBlock, linkOptions);
            filterBlock.LinkTo(flattenBlock, linkOptions);
            flattenBlock.LinkTo(playerCorrelatorBlock, linkOptions);
            playerCorrelatorBlock.LinkTo(outputTransformBlock, linkOptions);
            outputTransformBlock.LinkTo(listAppendBlock, linkOptions);

            await eventStreamerBlock.StartStreaming();

            await listAppendBlock.Completion;
            eventStreamerBlock.OnStreamProgressUpdated -= streamProgressDelegate;

            return finishedList;
        }

        private async Task<OutputRecord> CreateOutputRecord(PlayerFacilityEvent playerEvent)
        {
            var facilityInfo = await facilityInfoService.GetFacilityInfo(playerEvent.FacilityId);
            return new OutputRecord
            {
                CharacterId = playerEvent.CharacterId,
                CharacterName = outfitInfo.GetCharacterName(playerEvent.CharacterId) ?? "",
                CaptureTimestamp = playerEvent.Timestamp,
                FacilityId = playerEvent.FacilityId,
                FacilityName = facilityInfo.FacilityName,
                FacilityTypeId = facilityInfo.FacilityTypeId,
                FacilityType = facilityInfo.FacilityType,
            };
        }

    }
}
