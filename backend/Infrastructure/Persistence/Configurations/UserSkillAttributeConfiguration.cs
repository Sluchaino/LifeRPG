using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Configurations
{
    public sealed class UserSkillAttributeConfiguration : IEntityTypeConfiguration<UserSkillAttribute>
    {
        public void Configure(EntityTypeBuilder<UserSkillAttribute> builder)
        {
            builder.ToTable("user_skill_attributes", tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_user_skill_attributes_no_discipline",
                    "\"AttributeType\" <> 'Discipline'");
                tableBuilder.HasCheckConstraint(
                    "ck_user_skill_attributes_share_percent_range",
                    "\"SharePercent\" > 0 AND \"SharePercent\" <= 100");
            });

            builder.HasKey(x => new { x.UserSkillId, x.AttributeType });

            builder.Property(x => x.AttributeType)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(x => x.SharePercent)
                .IsRequired();
        }
    }
}
