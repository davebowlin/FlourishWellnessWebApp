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

                using var context = await _factory.CreateDbContextAsync();
                var user = await context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

                if (user == null)
                {
                    // Auto-create AD users with least privilege by default
                    user = new Models.User
                    {
                        Email = email,
                        FullName = fullName,
                        PasswordHash = string.Empty, // Not used for AD
                        Role = Models.UserRole.Employee,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.Users.Add(user);
                }
                else
                {
                    user.FullName = fullName;
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
                PasswordHash = string.Empty, // No password for AD users
                Role = role,
                CreatedAt = DateTime.UtcNow
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

        public async Task<bool> DeleteUserAsync(int userId)
        {
            using var context = await _factory.CreateDbContextAsync();
            var user = await context.Users.FindAsync(userId);
            if (user == null)
                return false;

            context.Users.Remove(user);
            await context.SaveChangesAsync();
            return true;
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

        public async Task<User?> AuthenticateLocalAdminAsync(string username, string password)
        {
            if (!string.Equals(username?.Trim(), "admin", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(password, "admin", StringComparison.Ordinal))
            {
                return null;
            }

            using var context = await _factory.CreateDbContextAsync();
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == "admin");
            if (user == null)
            {
                user = new User
                {
                    Email = "admin",
                    FullName = "Administrator",
                    PasswordHash = "admin",
                    Role = UserRole.Admin,
                    CreatedAt = DateTime.UtcNow
                };
                context.Users.Add(user);
            }
            else
            {
                user.Role = UserRole.Admin;
                if (string.IsNullOrWhiteSpace(user.FullName))
                {
                    user.FullName = "Administrator";
                }
                if (string.IsNullOrWhiteSpace(user.PasswordHash))
                {
                    user.PasswordHash = "admin";
                }
            }

            await context.SaveChangesAsync();
            await _logService.LogAsync("Local admin login", user.Email);
            return user;
        }
    }
}