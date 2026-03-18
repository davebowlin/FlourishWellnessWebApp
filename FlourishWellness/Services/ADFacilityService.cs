using FlourishWellness.Data;
using FlourishWellness.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FlourishWellness.Services
{
    public class ADFacilityService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ADFacilityService(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<ADFacilityUser>> GetFacilitiesForSamAccountAsync(string samAccountName)
        {
            if (string.IsNullOrWhiteSpace(samAccountName))
            {
                return new List<ADFacilityUser>();
            }

            using var context = await _factory.CreateDbContextAsync();

            var parameter = new SqlParameter("@samAccountName", samAccountName.Trim());
            var results = await context.Database
                .SqlQueryRaw<ADFacilityUser>(@"
SELECT DISTINCT
    Facility,
    CommunityKey,
    SAMAccountName
FROM [AmericareDW].[dbo].[FlourishADUsers]
WHERE SAMAccountName = @samAccountName
  AND ISNULL(Facility, '') <> ''", parameter)
                .ToListAsync();

            return results;
        }

        public async Task<List<ADFacilityUser>> GetOrSyncFacilitiesAsync(string samAccountName)
        {
            if (string.IsNullOrWhiteSpace(samAccountName))
            {
                return new List<ADFacilityUser>();
            }

            using var context = await _factory.CreateDbContextAsync();
            var sam = samAccountName.Trim();

            // Check local Community table first
            var cached = await context.Community
                .Where(c => c.SAMAccountName == sam)
                .ToListAsync();

            // If we have valid (non-empty Facility) rows cached, return them
            if (cached.Count > 0 && cached.All(c => !string.IsNullOrWhiteSpace(c.Facility)))
            {
                return cached.Select(c => new ADFacilityUser
                {
                    SAMAccountName = c.SAMAccountName,
                    Facility = c.Facility,
                    CommunityKey = c.CommunityKey.ToString()
                }).ToList();
            }

            // No valid cached data — try AmericareDW
            List<ADFacilityUser> adResults;
            try
            {
                adResults = await GetFacilitiesForSamAccountAsync(sam);
            }
            catch
            {
                // AmericareDW unreachable — return whatever we have cached (may be empty)
                return cached.Select(c => new ADFacilityUser
                {
                    SAMAccountName = c.SAMAccountName,
                    Facility = c.Facility,
                    CommunityKey = c.CommunityKey.ToString()
                }).ToList();
            }

            if (adResults.Count == 0)
            {
                return new List<ADFacilityUser>();
            }

            // Upsert results into dbo.Community
            foreach (var ad in adResults)
            {
                var existing = cached.FirstOrDefault(c =>
                    string.Equals(c.Facility, ad.Facility, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c.CommunityKey.ToString(), ad.CommunityKey ?? string.Empty, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    context.Community.Add(new Community
                    {
                        SAMAccountName = sam,
                        Facility = ad.Facility,
                        CommunityKey = int.TryParse(ad.CommunityKey, out var ck) ? ck : 0
                    });
                }
            }

            await context.SaveChangesAsync();
            return adResults;
        }
    }
}
