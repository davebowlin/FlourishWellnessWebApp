using FlourishWellness.Models;
using FlourishWellness.Data;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace FlourishWellness.Services
{
    public class AuthStateService
    {
        private readonly ProtectedSessionStorage _sessionStorage;
        private readonly LogService _logService;
        private readonly IDbContextFactory<AppDbContext> _dbFactory;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly AuthService _authService;
        private User? _currentUser;
        private bool _isInitialized = false;

        public event Action? OnAuthStateChanged;

        public AuthStateService(
            ProtectedSessionStorage sessionStorage,
            LogService logService,
            IDbContextFactory<AppDbContext> dbFactory,
            AuthenticationStateProvider authStateProvider,
            AuthService authService)
        {
            _sessionStorage = sessionStorage;
            _logService = logService;
            _dbFactory = dbFactory;
            _authStateProvider = authStateProvider;
            _authService = authService;
        }

        public User? CurrentUser
        {
            get => _currentUser;
            private set
            {
                _currentUser = value;
                OnAuthStateChanged?.Invoke();
            }
        }

        public bool IsAuthenticated => CurrentUser != null;
        public bool IsAdmin => CurrentUser?.Role == UserRole.Admin;
        public bool IsManager => CurrentUser?.Role == UserRole.Manager;
        public bool IsEmployee => CurrentUser?.Role == UserRole.Employee;

        public async Task InitializeAsync()
        {
            if (_isInitialized && CurrentUser != null)
                return;

            if (!_isInitialized)
            {
                try
                {
                    var result = await _sessionStorage.GetAsync<string>("currentUser");
                    if (result.Success && !string.IsNullOrEmpty(result.Value))
                    {
                        CurrentUser = JsonSerializer.Deserialize<User>(result.Value);
                    }
                }
                catch
                {
                    // Session storage not available yet
                }
            }

            if (CurrentUser != null)
            {
                await RefreshCurrentUserAsync();
            }

            if (CurrentUser == null)
            {
                try
                {
                    var authState = await _authStateProvider.GetAuthenticationStateAsync();
                    var user = authState.User;
                    if (user.Identity?.IsAuthenticated == true)
                    {
                        var dbUser = await _authService.AuthenticateWindowsUserAsync(user);
                        if (dbUser != null)
                        {
                            await SetUserAsync(dbUser);
                        }
                        else
                        {
                            await _logService.LogAsync("AuthenticateWindowsUserAsync returned null", user.Identity.Name ?? "Unknown");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log error instead of ignoring
                    await _logService.LogAsync($"Auto-login error: {ex.Message}", "System");
                    Console.WriteLine($"Auto-login error: {ex}");
                }
            }

            _isInitialized = true;
        }

        public async Task RefreshCurrentUserAsync()
        {
            if (CurrentUser == null || string.IsNullOrWhiteSpace(CurrentUser.Email))
            {
                return;
            }

            var dbUser = await _authService.GetUserByEmailAsync(CurrentUser.Email);
            if (dbUser != null)
            {
                await SetUserAsync(dbUser);
            }
            else
            {
                await LogoutAsync();
            }
        }

        public async Task InitializeFromWindowsAsync(string? windowsUsername)
        {
            if (string.IsNullOrEmpty(windowsUsername)) return;

            using var context = await _dbFactory.CreateDbContextAsync();

            // Check if user exists in DB by Windows Name (e.g. DOMAIN\User)
            var user = await context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == windowsUsername.ToLower());

            if (user != null)
            {
                await SetUserAsync(user);
            }
        }

        public async Task SetUserAsync(User user)
        {
            CurrentUser = user;
            var json = JsonSerializer.Serialize(user);
            await _sessionStorage.SetAsync("currentUser", json);
        }

        public async Task LogoutAsync()
        {
            if (CurrentUser != null)
            {
                await _logService.LogAsync("User logged out", CurrentUser.Email);
            }
            CurrentUser = null;
            await _sessionStorage.DeleteAsync("currentUser");
        }
    }
}