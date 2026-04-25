using System;

namespace Domain.Entities
{
    public sealed class HabitCompletion
    {
        public Guid HabitId { get; set; }
        public DateOnly Date { get; set; }
        public DateTime CompletedAtUtc { get; set; }

        public Habit Habit { get; set; } = default!;
    }
}
