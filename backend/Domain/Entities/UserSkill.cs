using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public sealed class UserSkill
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; } = default!;
        public string NormalizedName { get; set; } = default!;
        public string? Description { get; set; }

        public int Level { get; set; }
        public int CurrentUses { get; set; }
        public int RequiredUsesForNextLevel { get; set; }
        public DateOnly? LastUsedOn { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        public User User { get; set; } = default!;
        public List<UserSkillAttribute> Attributes { get; set; } = new();
    }
}
