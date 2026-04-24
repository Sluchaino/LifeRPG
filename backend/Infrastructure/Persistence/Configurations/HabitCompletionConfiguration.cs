using System;

namespace Infrastructure.Persistence.Configurations
{
    public sealed class HabitCompletionConfiguration : IEntityTypeConfiguration<HabitCompletion>
    {
        public void Configure(EntityTypeBuilder<HabitCompletion> builder)
        {
            builder.ToTable("habit_completions");

            builder.HasKey(x => new { x.HabitId, x.Date });

            builder.Property(x => x.CompletedAtUtc)
                .IsRequired();

            builder.HasIndex(x => x.Date);
        }
    }
}
