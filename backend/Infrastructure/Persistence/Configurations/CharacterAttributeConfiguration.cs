using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Configurations
{
    public sealed class CharacterAttributeConfiguration : IEntityTypeConfiguration<CharacterAttribute>
    {
        public void Configure(EntityTypeBuilder<CharacterAttribute> builder)
        {
            builder.ToTable("character_attributes");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.AttributeType)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(x => x.Value)
                .IsRequired();

            builder.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            builder.HasIndex(x => new { x.ProfileId, x.AttributeType })
                .IsUnique();
        }
    }
}
