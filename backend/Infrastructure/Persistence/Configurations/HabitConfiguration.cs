using System;

namespace Infrastructure.Persistence.Configurations
{
    public sealed class HabitConfiguration : IEntityTypeConfiguration<Habit>
    {
        public void Configure(EntityTypeBuilder<Habit> builder)
        {
            builder.ToTable("habits");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(120);

            builder.Property(x => x.NormalizedName)
                .IsRequired()
                .HasMaxLength(120);

            builder.Property(x => x.Description)
                .HasMaxLength(400);

            builder.Property(x => x.CreatedAtUtc)
                .IsRequired();

            builder.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            builder.HasIndex(x => new { x.UserId, x.NormalizedName })
                .IsUnique();

            builder.HasMany(x => x.Completions)
                .WithOne(x => x.Habit)
                .HasForeignKey(x => x.HabitId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
