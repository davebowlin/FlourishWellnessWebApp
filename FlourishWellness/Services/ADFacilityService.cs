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
    }
}
