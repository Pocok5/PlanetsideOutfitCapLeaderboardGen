using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CapLeaderboardGen.DataTypes
{
    internal record OutfitInfo
    {
        public required string Alias { get; init; }
        public int MemberCount { get; init; }
        public required string Name { get; init; }
        public long OutfitId { get; init; }
    }

    internal record OutfitInfoResponse : OutfitInfo
    {
        public record MemberRecord
        {
            public long CharacterId { get; init; }
            public CharacterName? Name { get; init; }
        }
        public record CharacterName
        {
            public required string First { get; init; }
        }

        public required MemberRecord[] Members { get; init; }
    }
}
