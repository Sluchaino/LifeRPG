using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence
{
    public static class SeedData
    {
        public static async Task InitializeAsync(AppDbContext dbContext)
        {
            var profileIds = await dbContext.CharacterProfiles
                .AsNoTracking()
                .Select(x => x.Id)
                .ToListAsync();

            if (profileIds.Count == 0)
            {
                return;
            }

            var existingAttributesByProfile = await dbContext.CharacterAttributes
                .AsNoTracking()
                .Where(x => profileIds.Contains(x.ProfileId))
                .GroupBy(x => x.ProfileId)
                .ToDictionaryAsync(
                    x => x.Key,
                    x => x
                        .Select(y => y.AttributeType)
                        .ToHashSet());

            var allAttributeTypes = Enum.GetValues<AttributeType>();
            var now = DateTime.UtcNow;
            var attributesToInsert = new List<CharacterAttribute>();

            foreach (var profileId in profileIds)
            {
                var existingAttributes = existingAttributesByProfile.TryGetValue(profileId, out var set)
                    ? set
                    : new HashSet<AttributeType>();

                foreach (var attributeType in allAttributeTypes)
                {
                    if (existingAttributes.Contains(attributeType))
                    {
                        continue;
                    }

                    attributesToInsert.Add(new CharacterAttribute
                    {
                        Id = Guid.NewGuid(),
                        ProfileId = profileId,
                        AttributeType = attributeType,
                        Value = 0,
                        UpdatedAtUtc = now
                    });
                }
            }

            if (attributesToInsert.Count > 0)
            {
                dbContext.CharacterAttributes.AddRange(attributesToInsert);
                await dbContext.SaveChangesAsync();
            }
        }
    }
}
