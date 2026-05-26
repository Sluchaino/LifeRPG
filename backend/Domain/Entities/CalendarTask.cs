using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public sealed class CalendarTask
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public DateOnly Date { get; set; }
        public string Title { get; set; } = default!;
        public string? Details { get; set; }
        public TaskImportance Importance { get; set; }
        public Difficulty Difficulty { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public int ExperienceAwarded { get; set; }
        public bool IsFirstTaskBonusApplied { get; set; }
        public Guid? HabitId { get; set; }
        public TimeOnly? StartTime { get; set; }
        public TimeOnly? EndTime { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        public User User { get; set; } = default!;
        public Habit? Habit { get; set; }
        public List<CalendarTaskAttribute> Attributes { get; set; } = new();
        public List<CalendarTaskSkill> Skills { get; set; } = new();
    }
}
