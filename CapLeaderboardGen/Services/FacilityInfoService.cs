using BitFaster.Caching.Lru;
using CapLeaderboardGen.DataTypes;
using CapLeaderboardGen.DependencyInjection;
using DbgCensus.Rest.Abstractions;
using DbgCensus.Rest.Abstractions.Queries;
using Microsoft.Extensions.Logging;
using Polly;

namespace CapLeaderboardGen.Services
{
    internal class FacilityInfoService
    {
        private readonly IQueryService queryService;
        private readonly CensusConfigContainer configContainer;
        private readonly ILogger<FacilityInfoService> logger;
        private readonly FastConcurrentLru<int, FacilityInfo> facilityCache = new(2048);
        private readonly IAsyncPolicy retryPolicy;

        public FacilityInfoService(IQueryService queryService, CensusConfigContainer configContainer, ILogger<FacilityInfoService> logger)
        {
            this.queryService = queryService;
            this.configContainer = configContainer;
            this.logger = logger;
            retryPolicy = Policy.Handle<HttpRequestException>()
                                .Or<TaskCanceledException>()
                               .WaitAndRetryAsync(6, (cnt) => TimeSpan.FromSeconds(Math.Pow(2, cnt)), (ex, retry) =>
                               {
                                   logger.LogError(ex, "HTTP exception");
                                   logger.LogError("Failed to query facility info, will retry in {RetryTime}", retry);
                               });
        }

        public async Task<FacilityInfo> GetFacilityInfo(int facilityId)
        {
            if (facilityCache.TryGet(facilityId, out var facilityInfo))
            {
                logger.LogDebug("Serving facility info for {FacilityId}/{Name} from cache", facilityId, facilityInfo.FacilityName);
                return facilityInfo;
            }
            else
            {
                logger.LogDebug("Downloading facility info for {FacilityId}", facilityId);
                var query = queryService.CreateQuery(configContainer.GetQueryOptions())
                    .OnCollection("map_region")
                    .Where("facility_id", SearchModifier.Equals, facilityId)
                    .WithLimit(1);

                var result = await retryPolicy.ExecuteAsync(async () => await queryService.GetAsync<FacilityInfo>(query));
                if (result != null)
                {
                    facilityCache.AddOrUpdate(facilityId, result);
                    logger.LogDebug("Downloaded facility info for {FacilityId}/{Name}", facilityId, result.FacilityName);
                    return result;
                }
                else
                {
                    throw new InvalidOperationException($"Facility ID {facilityId} not found in the API");
                }
            }
        }
    }
}
