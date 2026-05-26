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
            });

            builder.HasKey(x => new { x.UserSkillId, x.AttributeType });

            builder.Property(x => x.AttributeType)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();
        }
    }
}
