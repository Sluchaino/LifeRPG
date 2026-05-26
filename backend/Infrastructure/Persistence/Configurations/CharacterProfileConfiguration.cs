using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Configurations
{
    public sealed class CharacterProfileConfiguration : IEntityTypeConfiguration<CharacterProfile>
    {
        public void Configure(EntityTypeBuilder<CharacterProfile> builder)
        {
            builder.ToTable("character_profiles");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Level)
                .IsRequired()
                .HasDefaultValue(1);

            builder.Property(x => x.TotalExperience)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(x => x.CreatedAtUtc)
                .IsRequired();

            builder.HasIndex(x => x.UserId)
                .IsUnique();

            builder.HasMany(x => x.Attributes)
                .WithOne(x => x.Profile)
                .HasForeignKey(x => x.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
