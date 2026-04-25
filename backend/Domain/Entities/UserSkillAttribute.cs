using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public sealed class UserSkillAttribute
    {
        public Guid UserSkillId { get; set; }
        public AttributeType AttributeType { get; set; }
        public int SharePercent { get; set; }

        public UserSkill UserSkill { get; set; } = default!;
    }
}
