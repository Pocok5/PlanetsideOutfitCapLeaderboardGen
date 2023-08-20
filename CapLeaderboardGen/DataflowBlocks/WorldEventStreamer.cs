using BitFaster.Caching.Lru;
using CapLeaderboardGen.DataTypes;
using DbgCensus.Rest;
using DbgCensus.Rest.Abstractions;
using DbgCensus.Rest.Abstractions.Queries;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace CapLeaderboardGen.DataflowBlocks
{
    internal class WorldEventStreamer : IReceivableSourceBlock<WorldFacilityEvent[]>
    {
        public record ProgressUpdateEventArgs
        {
            public DateTimeOffset CurrentTimestamp { get; init; }
            public double ProgressPercentage { get; init; }
        }

        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly BufferBlock<WorldFacilityEvent[]> sourceBuffer = new BufferBlock<WorldFacilityEvent[]>();
        
        private readonly int world;
        private readonly IQueryService queryService;
        private readonly CensusQueryOptions queryOptions;
        private readonly ILogger? logger;
        private readonly IAsyncPolicy retryPolicy;
        private readonly FastConcurrentLru<string, bool> dedupCache=new(2048);

        public DateTimeOffset EndDate { get; }
        public DateTimeOffset StartDate { get; }
        public event Action<ProgressUpdateEventArgs>? OnStreamProgressUpdated;

        public WorldEventStreamer(DateTimeOffset startDate, DateTimeOffset endDate, int world, IQueryService queryService, CensusQueryOptions queryOptions, ILogger? logger)
        {
            EndDate = endDate;
            this.world = world;
            this.queryService = queryService;
            this.queryOptions = queryOptions;
            this.logger = logger;
            StartDate = startDate;

            retryPolicy = Policy
                            .Handle<HttpRequestException>()
                            .WaitAndRetryAsync(6, (cnt) => TimeSpan.FromSeconds(Math.Pow(2, cnt)), (ex, retryTime) =>
                            {
                                logger?.LogError(ex, "HTTP exception occured");
                                logger?.LogError("Couldn't retrieve a page of facility capture events, will retry in {RetryTime}", retryTime);
                            });
        }

        public async Task StartStreaming()
        {
            const int pageSize = 1000;

            var endTimestamp = StartDate.ToUnixTimeSeconds();
            var currentTimestamp = EndDate.ToUnixTimeSeconds();
            var returnedItemsCount = 0;

            do
            {
                logger?.LogDebug("Attempting to retrieve capture events before {NextStartTimestamp}", DateTimeOffset.FromUnixTimeSeconds(currentTimestamp));
                var query = queryService.CreateQuery(queryOptions)
                    .OnCollection("world_event")
                    .Where("type", SearchModifier.Equals, "facility")
                    .Where("world_id", SearchModifier.Equals, world)
                    .Where("after", SearchModifier.Equals, endTimestamp)
                    .Where("before", SearchModifier.Equals, currentTimestamp)
                    .WithLimit(pageSize);

                var response = await retryPolicy.ExecuteAsync(
                    async() => await queryService.GetAsync<WorldFacilityEvent[]>(query, cancellationTokenSource.Token)
                  );

                if (response is null)
                {
                    break; //We presumably got to the end
                }
                else {
                    returnedItemsCount = response.Length;
                }

                var earliestTimestamp = response.OrderBy(x => x.Timestamp).Take(1).Select(x=>x.Timestamp).FirstOrDefault();
                var earliestTSDate = DateTimeOffset.FromUnixTimeSeconds(earliestTimestamp);
                OnStreamProgressUpdated?.Invoke(new ProgressUpdateEventArgs { 
                    CurrentTimestamp = earliestTSDate,
                    ProgressPercentage = (EndDate-earliestTSDate)/(EndDate - StartDate)*100
                });
                currentTimestamp = earliestTimestamp;

                var deduped = DeduplicateEvents(response);

                sourceBuffer.Post(deduped);

            } while (returnedItemsCount == pageSize && !cancellationTokenSource.IsCancellationRequested);
        }

        private WorldFacilityEvent[] DeduplicateEvents(WorldFacilityEvent[] response)
        {
            var deduped = response.Where(elem => !dedupCache.TryGet(elem.GetCacheKey(), out _)).ToArray();
            foreach (var elem in deduped)
            {
                dedupCache.AddOrUpdate(elem.GetCacheKey(), true);
            }
            if (response.Length != deduped.Length)
            {
                logger?.LogInformation("{Duplicates} duplicate events were dropped from this batch.", response.Length-deduped.Length);
            }
            return deduped;
        }

        #region Sourceblock boilerplate
        public Task Completion => sourceBuffer.Completion;

        public void Complete()
        {
            cancellationTokenSource.Cancel();
            sourceBuffer.Complete();
        }

        public WorldFacilityEvent[]? ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<WorldFacilityEvent[]> target, out bool messageConsumed)
        {
            return ((IReceivableSourceBlock<WorldFacilityEvent[]>)sourceBuffer).ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public void Fault(Exception exception)
        {
            ((IReceivableSourceBlock<WorldFacilityEvent[]>)sourceBuffer).Fault(exception);
        }

        public IDisposable LinkTo(ITargetBlock<WorldFacilityEvent[]> target, DataflowLinkOptions linkOptions)
        {
            return sourceBuffer.LinkTo(target, linkOptions);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<WorldFacilityEvent[]> target)
        {
            ((IReceivableSourceBlock<WorldFacilityEvent[]>)sourceBuffer).ReleaseReservation(messageHeader, target);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<WorldFacilityEvent[]> target)
        {
            return ((IReceivableSourceBlock<WorldFacilityEvent[]>)sourceBuffer).ReserveMessage(messageHeader, target);
        }

        public bool TryReceive(Predicate<WorldFacilityEvent[]>? filter, [MaybeNullWhen(false)] out WorldFacilityEvent[] item)
        {
            return sourceBuffer.TryReceive(filter, out item);
        }

        public bool TryReceiveAll([NotNullWhen(true)] out IList<WorldFacilityEvent[]>? items)
        {
            return sourceBuffer.TryReceiveAll(out items);
        }
        #endregion
    }
}
