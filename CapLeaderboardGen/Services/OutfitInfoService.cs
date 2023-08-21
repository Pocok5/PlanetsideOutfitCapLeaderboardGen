using CapLeaderboardGen.DataTypes;
using CapLeaderboardGen.DependencyInjection;
using DbgCensus.Rest.Abstractions;
using DbgCensus.Rest.Abstractions.Queries;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace CapLeaderboardGen.Services
{
    internal class OutfitInfoService
    {
        private readonly CensusConfigContainer configContainer;
        private readonly IQueryService queryService;
        private readonly ILogger<OutfitInfoService> logger;

        private readonly AsyncRetryPolicy retryPolicy;

        public OutfitInfo? OutfitInfo { get; private set; }
        private Dictionary<long, string> characterNames = new();


        public OutfitInfoService(CensusConfigContainer configContainer, IQueryService queryService, ILogger<OutfitInfoService> logger)
        {
            this.configContainer = configContainer;
            this.queryService = queryService;
            this.logger = logger;

            retryPolicy = Policy
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(6, (cnt) => TimeSpan.FromSeconds(Math.Pow(2, cnt)), (ex, retryTime) =>
                {
                    logger.LogError(ex, "HTTP exception occured");
                    logger.LogError("Couldn't retrieve outfit info, will retry in {RetryTime}", retryTime);
                });
        }

        public async Task RetrieveOutfitData(string outfitTag)
        {
            var query = queryService.CreateQuery(configContainer.GetQueryOptions())
                .OnCollection("outfit")
                .WithLimit(1)
                .Where("alias_lower", SearchModifier.Equals, outfitTag.ToLower())
                .AddResolve("member_character_name");

            var response = await retryPolicy.ExecuteAsync(async()=> await queryService.GetAsync<OutfitInfoResponse>(query));

            if (response == null)
            {

                logger.LogCritical("Failed to retrieve or parse outfit info");
                throw new InvalidOperationException("Failed to retrieve or parse outfit info");
            }

            logger.LogInformation("Retrieved data of outfit {Tag} {Name}. Retrieved names and IDs of {MembersArrayCount}/{Members} members.",
                response.Alias,
                response.Name,
                response.Members.Length,
                response.MemberCount
                );

            if (response.Members.Length != response.MemberCount)
            {
                logger.LogWarning("Couldn't retrieve every member of the outfit. {Difference}/{TotalMembers} are missing, results may be inaccurate.",
                    response.MemberCount-response.Members.Length, 
                    response.MemberCount
                );
            }

            var filtered = response with
            {
                Members = response.Members.Where(member => member.Name is not null).ToArray()
            };

            var spookyGhosts = response.Members.Length - filtered.Members.Length;
            if (spookyGhosts > 0)
            {
                logger.LogWarning("{NonexistentPlayers} members of the outfit are nameless ghost records [grey](thanks Daybreak very cool)[/]", spookyGhosts);
            }
            
            BuildCharacterNameCache(filtered);
            OutfitInfo = new OutfitInfo
            {
                Alias= filtered.Alias,
                Name = filtered.Name,
                MemberCount= filtered.MemberCount,
                OutfitId = filtered.OutfitId
            };
        }

        private void BuildCharacterNameCache(OutfitInfoResponse outfitInfo)
        {
            characterNames = outfitInfo.Members.ToDictionary(
                keySelector: (rec) => rec.CharacterId,
                elementSelector: (rec) => rec.Name?.First ?? throw new InvalidOperationException("A nameless character sneaked past the check")
            );
        }

        public string? GetCharacterName(long characterId)
        {
            if (characterNames.TryGetValue(characterId, out var characterName))
            {
                return characterName;
            }
            else
            {
                return null;
            }
        }
    }
}
