using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Configurations
{
    public sealed class CalendarTaskSkillConfiguration : IEntityTypeConfiguration<CalendarTaskSkill>
    {
        public void Configure(EntityTypeBuilder<CalendarTaskSkill> builder)
        {
            builder.ToTable("calendar_task_skills");

            builder.HasKey(x => new { x.CalendarTaskId, x.UserSkillId });

            builder.HasOne(x => x.CalendarTask)
                .WithMany(x => x.Skills)
                .HasForeignKey(x => x.CalendarTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.UserSkill)
                .WithMany()
                .HasForeignKey(x => x.UserSkillId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
