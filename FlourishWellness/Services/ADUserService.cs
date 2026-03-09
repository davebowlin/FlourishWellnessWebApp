using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Security.Claims;

namespace FlourishWellness.Services
{
    public class ADUserService
    {
        public string? GetSamAccountName(ClaimsPrincipal user)
        {
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var sam = user.FindFirst("samaccountname")?.Value
                ?? user.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value
                ?? user.FindFirst(ClaimTypes.Name)?.Value
                ?? user.Identity?.Name;

            if (string.IsNullOrWhiteSpace(sam))
            {
                return null;
            }

            sam = sam.Trim();
            if (sam.Contains('\\'))
            {
                sam = sam.Split('\\')[1];
            }

            return sam;
        }

        public string? GetExtensionAttribute10(ClaimsPrincipal user, string? samAccountName = null)
        {
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var extensionAttribute10 = user.FindFirst("extensionAttribute10")?.Value
                ?? user.FindFirst("extensionattribute10")?.Value;

            if (!string.IsNullOrWhiteSpace(extensionAttribute10))
            {
                return extensionAttribute10.Trim();
            }

            var sam = samAccountName ?? GetSamAccountName(user);
            if (string.IsNullOrWhiteSpace(sam))
            {
                return null;
            }

            return GetExtensionAttribute10FromDirectory(sam);
        }

        private string? GetExtensionAttribute10FromDirectory(string samAccountName)
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            try
            {
                using var context = new PrincipalContext(ContextType.Domain);
                using var principal = UserPrincipal.FindByIdentity(
                    context,
                    IdentityType.SamAccountName,
                    samAccountName
                );

                if (principal?.GetUnderlyingObject() is not DirectoryEntry directoryEntry)
                {
                    return null;
                }

                var rawValue = directoryEntry.Properties["extensionAttribute10"]?.Value?.ToString();
                return string.IsNullOrWhiteSpace(rawValue) ? null : rawValue.Trim();
            }
            catch
            {
                return null;
            }
        }

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