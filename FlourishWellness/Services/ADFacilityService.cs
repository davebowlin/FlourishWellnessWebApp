using FlourishWellness.Data;
using FlourishWellness.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FlourishWellness.Services
{
    public class ADFacilityService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly LogService _logService;

        public ADFacilityService(IDbContextFactory<AppDbContext> factory, LogService logService)
        {
            _factory = factory;
            _logService = logService;
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
                      AND ISNULL(Facility, '') <> ''",
                    parameter)
                .ToListAsync();

            return results;

        }

        public async Task<List<ADFacilityUser>> GetOrSyncFacilitiesAsync(string samAccountName)
        {
            if (string.IsNullOrWhiteSpace(samAccountName))
            {
                return new List<ADFacilityUser>();
            }

            var sam = samAccountName.Trim();

            // Always query AD first so newly-added facility assignments are picked up immediately.
            // The local Community table is only used as a fallback when AD is unreachable.
            List<ADFacilityUser> adResults;
            try
            {
                adResults = await GetFacilitiesForSamAccountAsync(sam);
            }
            catch (Exception ex)
            {
                await _logService.LogAsync($"ADFacilityService error for '{sam}': {ex.Message}", "System");
                adResults = new List<ADFacilityUser>();
            }

            if (adResults.Count > 0)
            {
                // Sync any new entries into the local Community cache
                using var context = await _factory.CreateDbContextAsync();
                var cached = await context.Community
                    .Where(c => c.SAMAccountName == sam)
                    .ToListAsync();

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

            // AD unreachable or returned no results — fall back to local cache
            using var fallbackContext = await _factory.CreateDbContextAsync();
            return (await fallbackContext.Community
                .Where(c => c.SAMAccountName == sam && c.Facility != "")
                .ToListAsync())
                .Select(c => new ADFacilityUser
                {
                    SAMAccountName = c.SAMAccountName,
                    Facility = c.Facility,
                    CommunityKey = c.CommunityKey.ToString()
                }).ToList();
        }
    }
}
