using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public sealed class CharacterProfile
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public int Level { get; set; }
        public int TotalExperience { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public User User { get; set; } = default!;
        public List<CharacterAttribute> Attributes { get; set; } = new();
    }
}
