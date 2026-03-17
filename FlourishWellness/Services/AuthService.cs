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
                        PasswordHash = string.Empty, // Not used for AD
                        Role = isFirstUser ? Models.UserRole.Admin : Models.UserRole.Employee,
                        CreatedAt = DateTime.UtcNow
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

        public async Task<User?> GetUserBySamAccountNameAsync(string samAccountName)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Users.FirstOrDefaultAsync(u => u.SAMAccountName.ToLower() == samAccountName.ToLower());
        }

        public async Task<User?> AuthenticateLocalAdminAsync(string username, string password)
        {
            if (!string.Equals(username?.Trim(), "admin", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(password, "admin", StringComparison.Ordinal))
            {
                return null;
            }

            using var context = await _factory.CreateDbContextAsync();
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == "admin");
            if (user == null)
            {
                return null;
            }

            await _logService.LogAsync("Local admin login", user.Email);
            return user;
        }

        /* public async Task<User?> AuthenticateLocalAdminAsync(string username, string password)
        {
            // IMPORTANT!! This is a fallback local admin account. In production, you should change the password
            // immediately after first login. This code ensures one admin can access the system even if AD is
            // unavailable or whatever. 
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
                    SAMAccountName = "admin",
                    //ExtensionAttribute10 = string.Empty,
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
                if (string.IsNullOrWhiteSpace(user.SAMAccountName))
                {
                    user.SAMAccountName = "admin";
                }
                if (string.IsNullOrWhiteSpace(user.ExtensionAttribute10))
                {
                    user.ExtensionAttribute10 = string.Empty;
                }
            }

            await context.SaveChangesAsync();
            await _logService.LogAsync("Local admin login", user.Email);
            return user; 
        }*/
    }
}