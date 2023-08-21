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
        public record ProgressUpdateEventArgs
        {
            public double DownloadProgressPercentage { get; init; }
            public bool PostProcessingInProgress { get; init; }
        }

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

        public async Task<List<OutputRecord>> ProcessEvents(DateTimeOffset startDate, DateTimeOffset endDate, int worldId, long outfitId, Action<ProgressUpdateEventArgs> progressDelegate)
        {
            var streamProgressEventHandler = (WorldEventStreamer.ProgressUpdateEventArgs args) =>
            {
                progressDelegate(new ProgressUpdateEventArgs
                {
                    DownloadProgressPercentage = args.ProgressPercentage,
                    PostProcessingInProgress = false
                });
            };

            var finishedList = new List<OutputRecord>();

            var eventStreamerBlock = blockFactory.CreateWorldEventStreamerBlock(worldId, startDate, endDate);
            eventStreamerBlock.OnStreamProgressUpdated += streamProgressEventHandler;

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
            await eventStreamerBlock.Completion;
            
            eventStreamerBlock.OnStreamProgressUpdated -= streamProgressEventHandler;
            progressDelegate(new ProgressUpdateEventArgs { DownloadProgressPercentage = 100, PostProcessingInProgress = true });

            await listAppendBlock.Completion;
            

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
