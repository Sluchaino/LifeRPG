using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    public sealed class Habit
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; } = default!;
        public string NormalizedName { get; set; } = default!;
        public string? Description { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        public User User { get; set; } = default!;
        public List<HabitCompletion> Completions { get; set; } = new();
    }
}
