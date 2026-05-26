using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Configurations
{
    public sealed class UserSkillConfiguration : IEntityTypeConfiguration<UserSkill>
    {
        public void Configure(EntityTypeBuilder<UserSkill> builder)
        {
            builder.ToTable("user_skills");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.NormalizedName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.Description)
                .HasMaxLength(300);

            builder.Property(x => x.Level).IsRequired();
            builder.Property(x => x.CurrentUses).IsRequired();
            builder.Property(x => x.RequiredUsesForNextLevel).IsRequired();
            builder.Property(x => x.StreakDays).IsRequired();
            builder.Property(x => x.CreatedAtUtc).IsRequired();
            builder.Property(x => x.UpdatedAtUtc).IsRequired();

            builder.HasIndex(x => new { x.UserId, x.NormalizedName })
                .IsUnique();

            builder.HasMany(x => x.Attributes)
                .WithOne(x => x.UserSkill)
                .HasForeignKey(x => x.UserSkillId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
