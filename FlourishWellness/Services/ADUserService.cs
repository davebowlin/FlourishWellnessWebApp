using System.Security.Claims;

namespace FlourishWellness.Services
{
    public class ADUserService
    {
        public string? GetFullName(ClaimsPrincipal user)
        {
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var displayName = user.FindFirst("displayName")?.Value;
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName.Trim();
            }

            var name = user.FindFirst(ClaimTypes.Name)?.Value;
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name.Trim();
            }

            var givenName = user.FindFirst(ClaimTypes.GivenName)?.Value;
            var surname = user.FindFirst(ClaimTypes.Surname)?.Value;
            var fullName = $"{givenName} {surname}".Trim();
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName;
            }

            var identityName = user.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(identityName))
            {
                return identityName;
            }

            return null;
        }
    }
}