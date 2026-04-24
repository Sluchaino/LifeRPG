using System;

namespace Domain.Entities
{
    public sealed class CalendarTaskSkill
    {
        public Guid CalendarTaskId { get; set; }
        public Guid UserSkillId { get; set; }

        public CalendarTask CalendarTask { get; set; } = default!;
        public UserSkill UserSkill { get; set; } = default!;
    }
}
