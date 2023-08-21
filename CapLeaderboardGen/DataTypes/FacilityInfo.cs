using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapLeaderboardGen.DataTypes
{
    internal record FacilityInfo
    {
        public record ResourceReward
        {
            public required string Description { get; init; }
            public int Amount { get; init; }
        }
        public int FacilityId { get; init; }
        public int ZoneId { get; init; }
        public required string FacilityName { get; init; }
        public required int FacilityTypeId { get; init; }
        public required string FacilityType { get; init;}
        public ResourceReward? CaptureReward { get; init; }
        public ResourceReward? TickReward { get; init; }
    }
}
