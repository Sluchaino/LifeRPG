using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public sealed class User
    {
        public Guid Id { get; set; }
        public string Login { get; set; } = default!;
        public string PasswordHash { get; set; } = default!;
        public DateTime CreatedAtUtc { get; set; }

        public CharacterProfile Profile { get; set; } = default!;
        public List<UserSkill> UserSkills { get; set; } = new();
        public List<CalendarTask> CalendarTasks { get; set; } = new();
        public List<Habit> Habits { get; set; } = new();
    }
}
