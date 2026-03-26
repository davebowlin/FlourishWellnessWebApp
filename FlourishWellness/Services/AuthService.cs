using System.Security.Claims;
using FlourishWellness.Data;
using FlourishWellness.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace FlourishWellness.Services
{
    public class AuthService
    {
        public sealed class DeleteUserResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public string DeletedUserEmail { get; set; } = string.Empty;
            public int DeletedResponses { get; set; }
            public int DeletedStatuses { get; set; }
            public int DeletedCommunities { get; set; }
        }

        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly LogService _logService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ADUserService _adUserService;

        public AuthService(IDbContextFactory<AppDbContext> factory, LogService logService, IHttpContextAccessor httpContextAccessor, ADUserService adUserService)
        {
            _factory = factory;
            _logService = logService;
            _httpContextAccessor = httpContextAccessor;
            _adUserService = adUserService;
        }

        // AuthenticateAsync method removed as we only use AD authentication

        // New method for AD/Windows authentication
        public async Task<User?> AuthenticateWindowsUserAsync()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated != true)
                return null;

            return await AuthenticateWindowsUserAsync(httpContext.User);
        }

        public async Task<User?> AuthenticateWindowsUserAsync(ClaimsPrincipal principal)
        {
            if (principal?.Identity?.IsAuthenticated != true)
                return null;

            try
            {
                var adName = principal.Identity?.Name; // DOMAIN\\username
                // Extract username from DOMAIN\username if present
                if (!string.IsNullOrEmpty(adName) && adName.Contains('\\'))
                {
                    adName = adName.Split('\\')[1];
                }

                if (string.IsNullOrEmpty(adName))
                {
                    adName = "UnknownUser";
                }

                var fullName = _adUserService.GetFullName(principal) ?? adName;
                var email = $"americare.org\\{adName}"; // Format: americare.org\username
                var samAccountName = _adUserService.GetSamAccountName(principal) ?? adName;
                // var extensionAttribute10 = _adUserService.GetExtensionAttribute10(principal, samAccountName);  # Not currently being used.

                using var context = await _factory.CreateDbContextAsync();
                var user = await context.Users.FirstOrDefaultAsync(u =>
                    u.SAMAccountName.ToLower() == samAccountName.ToLower());

                if (user == null)
                {
                    user = await context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
                }

                if (user == null)
                {
                    var isFirstUser = !await context.Users.AnyAsync();

                    // Auto-create AD users with least privilege by default
                    user = new Models.User
                    {
                        Email = email,
                        FullName = fullName,
                        SAMAccountName = samAccountName,
                        //ExtensionAttribute10 = string.Empty, // Not currently being used.
                        // = string.Empty, // Not used for AD
                        Role = isFirstUser ? Models.UserRole.Admin : Models.UserRole.Employee,
                        CreatedAt = Models.TimeHelper.CstNow
                    };
                    context.Users.Add(user);
                }
                else
                {
                    user.FullName = fullName;
                    user.SAMAccountName = samAccountName;
                    /*  if (!string.IsNullOrWhiteSpace(extensionAttribute10))  # Not currently being used.
                    {
                        user.ExtensionAttribute10 = extensionAttribute10;
                    } */
                }

                await context.SaveChangesAsync();

                await _logService.LogAsync($"AD User logged in: {fullName}", email);
                return user;
            }
            catch (Exception ex)
            {
                await _logService.LogAsync($"Error in AuthenticateWindowsUserAsync: {ex.Message}", "System");
                return null;
            }
        }

        public async Task<User?> CreateUserAsync(string email, UserRole role)
        {
            using var context = await _factory.CreateDbContextAsync();

            // Check if user already exists
            var existingUser = await context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

            if (existingUser != null)
                return null; // User already exists

            var newUser = new User
            {
                Email = email,
                FullName = email,
                SAMAccountName = email.Contains('\\') ? email.Split('\\')[1] : email,
                // ExtensionAttribute10 = string.Empty,
                // PasswordHash = string.Empty, // No password for AD users
                Role = role,
                CreatedAt = TimeHelper.CstNow
            };

            context.Users.Add(newUser);
            await context.SaveChangesAsync();

            return newUser;
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Users
                .OrderBy(u => u.Role)
                .ThenBy(u => u.Email)
                .ToListAsync();
        }

        public async Task<DeleteUserResult> DeleteUserWithRelatedDataAsync(int userId)
        {
            using var context = await _factory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync();

            var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return new DeleteUserResult
                {
                    Success = false,
                    Message = "User not found."
                };
            }

            var responses = await context.Responses
                .Where(r => r.UserId == userId)
                .ToListAsync();

            var statuses = await context.UserSurveyStatuses
                .Where(s => s.UserId == userId)
                .ToListAsync();

            var communities = await context.Community
                .Where(c => c.Id == userId)
                .ToListAsync();

            context.Responses.RemoveRange(responses);
            context.UserSurveyStatuses.RemoveRange(statuses);
            context.Community.RemoveRange(communities);
            context.Users.Remove(user);

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new DeleteUserResult
            {
                Success = true,
                Message = "User and related records were deleted successfully.",
                DeletedUserEmail = user.Email,
                DeletedResponses = responses.Count,
                DeletedStatuses = statuses.Count,
                DeletedCommunities = communities.Count
            };
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            var result = await DeleteUserWithRelatedDataAsync(userId);
            return result.Success;
        }

        public async Task<bool> UpdateUserRoleAsync(int userId, UserRole role)
        {
            using var context = await _factory.CreateDbContextAsync();
            var user = await context.Users.FindAsync(userId);
            if (user == null)
            {
                return false;
            }

            user.Role = role;
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        }

        public async Task<User?> GetUserBySamAccountNameAsync(string samAccountName)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Users.FirstOrDefaultAsync(u => u.SAMAccountName.ToLower() == samAccountName.ToLower());
        }
    }
}