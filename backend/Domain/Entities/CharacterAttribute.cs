using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public sealed class CharacterAttribute
    {
        public Guid Id { get; set; }
        public Guid ProfileId { get; set; }
        public AttributeType AttributeType { get; set; }
        public int Value { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        public CharacterProfile Profile { get; set; } = default!;
    }
}
