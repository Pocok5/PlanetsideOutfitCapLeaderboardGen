using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapLeaderboardGen.DataTypes
{
    internal record WorldFacilityEvent
    {
        public int FacilityId { get; init; }
        public int FactionOld { get; init; }
        public int FactionNew { get; init; }
        public long Timestamp { get; init; }
        public int ZoneId { get; init; }
        public int WorldId { get; init; }
        public long OutfitId { get; init; }
    }

    internal static class WorldFacilityEventExtensions
    {
        public static string GetCacheKey(this WorldFacilityEvent facilityEvent) => $"{facilityEvent.Timestamp}-{facilityEvent.FacilityId}-{facilityEvent.ZoneId}-{facilityEvent.WorldId}";
    }
}
