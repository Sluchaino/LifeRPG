using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Configurations
{
    public sealed class CalendarTaskConfiguration : IEntityTypeConfiguration<CalendarTask>
    {
        public void Configure(EntityTypeBuilder<CalendarTask> builder)
        {
            builder.ToTable("calendar_tasks");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Date)
                .IsRequired();

            builder.Property(x => x.Title)
                .IsRequired()
                .HasMaxLength(120);

            builder.Property(x => x.Details)
                .HasMaxLength(500);

            builder.Property(x => x.Difficulty)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(Difficulty.Medium)
                .IsRequired();

            builder.Property(x => x.IsCompleted)
                .HasDefaultValue(false)
                .IsRequired();

            builder.Property(x => x.StartTime);
            builder.Property(x => x.EndTime);

            builder.Property(x => x.CreatedAtUtc)
                .IsRequired();

            builder.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            builder.HasMany(x => x.Attributes)
                .WithOne(x => x.CalendarTask)
                .HasForeignKey(x => x.CalendarTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.Skills)
                .WithOne(x => x.CalendarTask)
                .HasForeignKey(x => x.CalendarTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => new { x.UserId, x.Date });
        }
    }
}
