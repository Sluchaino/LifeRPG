using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;

namespace Infrastructure.Persistence
{
    public sealed class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<CharacterProfile> CharacterProfiles => Set<CharacterProfile>();
        public DbSet<CharacterAttribute> CharacterAttributes => Set<CharacterAttribute>();
        public DbSet<UserSkill> UserSkills => Set<UserSkill>();
        public DbSet<UserSkillAttribute> UserSkillAttributes => Set<UserSkillAttribute>();
        public DbSet<CalendarTask> CalendarTasks => Set<CalendarTask>();
        public DbSet<CalendarTaskAttribute> CalendarTaskAttributes => Set<CalendarTaskAttribute>();
        public DbSet<CalendarTaskSkill> CalendarTaskSkills => Set<CalendarTaskSkill>();
        public DbSet<Habit> Habits => Set<Habit>();
        public DbSet<HabitCompletion> HabitCompletions => Set<HabitCompletion>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
}
