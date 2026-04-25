using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Configurations
{
    public sealed class CalendarTaskAttributeConfiguration : IEntityTypeConfiguration<CalendarTaskAttribute>
    {
        public void Configure(EntityTypeBuilder<CalendarTaskAttribute> builder)
        {
            builder.ToTable("calendar_task_attributes", tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_calendar_task_attributes_share_percent_range",
                    "\"SharePercent\" > 0 AND \"SharePercent\" <= 100");
            });

            builder.HasKey(x => new { x.CalendarTaskId, x.AttributeType });

            builder.Property(x => x.AttributeType)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(x => x.SharePercent)
                .IsRequired()
                .HasDefaultValue(100);
        }
    }
}
