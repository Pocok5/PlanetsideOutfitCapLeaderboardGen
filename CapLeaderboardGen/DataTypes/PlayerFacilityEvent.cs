using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapLeaderboardGen.DataTypes
{
    internal record PlayerFacilityEvent
    {
        public long CharacterId { get; init; }
        public int FacilityId { get; init; }
        public long OutfitId { get; init; }
        public long Timestamp { get; init; }
        public int ZoneId { get; init; }
        public int WorldId { get; init; }
    }
}
