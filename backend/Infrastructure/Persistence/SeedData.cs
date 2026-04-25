using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence
{
    public static class SeedData
    {
        public static async Task InitializeAsync(AppDbContext dbContext)
        {
            var profiles = await dbContext.CharacterProfiles
                .Include(x => x.Attributes)
                .ToListAsync();

            if (profiles.Count == 0)
            {
                return;
            }

            var allAttributeTypes = Enum.GetValues<AttributeType>();
            var now = DateTime.UtcNow;
            var hasChanges = false;

            foreach (var profile in profiles)
            {
                var existingAttributes = profile.Attributes
                    .Select(x => x.AttributeType)
                    .ToHashSet();

                foreach (var attributeType in allAttributeTypes)
                {
                    if (existingAttributes.Contains(attributeType))
                    {
                        continue;
                    }

                    profile.Attributes.Add(new CharacterAttribute
                    {
                        Id = Guid.NewGuid(),
                        ProfileId = profile.Id,
                        AttributeType = attributeType,
                        Value = 0,
                        UpdatedAtUtc = now
                    });
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                await dbContext.SaveChangesAsync();
            }
        }
    }
}
