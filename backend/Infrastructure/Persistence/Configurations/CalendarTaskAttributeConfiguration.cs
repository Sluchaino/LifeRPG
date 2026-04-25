using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Configurations
{
    public sealed class CalendarTaskAttributeConfiguration : IEntityTypeConfiguration<CalendarTaskAttribute>
    {
        public void Configure(EntityTypeBuilder<CalendarTaskAttribute> builder)
        {
            builder.ToTable("calendar_task_attributes");

            builder.HasKey(x => new { x.CalendarTaskId, x.AttributeType });

            builder.Property(x => x.AttributeType)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();
        }
    }
}
