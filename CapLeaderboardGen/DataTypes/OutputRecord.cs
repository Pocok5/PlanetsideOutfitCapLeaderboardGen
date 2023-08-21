using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapLeaderboardGen.DataTypes
{
    internal record OutputRecord
    {
        public long CharacterId { get; init; }
        public required string CharacterName { get; init; }
        public int FacilityId { get; init; }
        public required string FacilityName { get; init; }
        public int FacilityTypeId { get; init; }
        public required string FacilityType { get; init; }
        public DateTimeOffset CaptureDateTime => DateTimeOffset.FromUnixTimeSeconds(CaptureTimestamp);
        public long CaptureTimestamp { get; init; }
        
    }
}
