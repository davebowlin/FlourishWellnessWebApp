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
        private readonly ADFacilityService _adFacilityService;
        private User? _currentUser;
        private bool _isInitialized = false;
        private List<ADFacilityUser> _adFacilities = new();
        private ADFacilityUser? _selectedFacility;

        public event Action? OnAuthStateChanged;

        public AuthStateService(
            ProtectedSessionStorage sessionStorage,
            LogService logService,
            IDbContextFactory<AppDbContext> dbFactory,
            AuthenticationStateProvider authStateProvider,
            AuthService authService,
            ADFacilityService adFacilityService)
        {
            _sessionStorage = sessionStorage;
            _logService = logService;
            _dbFactory = dbFactory;
            _authStateProvider = authStateProvider;
            _authService = authService;
            _adFacilityService = adFacilityService;
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
        public IReadOnlyList<ADFacilityUser> ADFacilities => _adFacilities;
        public ADFacilityUser? SelectedFacility => _selectedFacility;
        public bool RequiresFacilitySelection => _adFacilities.Select(x => x.Facility).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1
            && _selectedFacility == null;

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
                await LoadADFacilitiesAsync();
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
                            await LoadADFacilitiesAsync();
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

        public async Task LoadADFacilitiesAsync()
        {
            _adFacilities = new List<ADFacilityUser>();
            _selectedFacility = null;

            if (CurrentUser == null || string.IsNullOrWhiteSpace(CurrentUser.SAMAccountName))
            {
                OnAuthStateChanged?.Invoke();
                return;
            }

            try
            {
                _adFacilities = await _adFacilityService.GetFacilitiesForSamAccountAsync(CurrentUser.SAMAccountName);

                if (_adFacilities.Count == 1)
                {
                    _selectedFacility = _adFacilities[0];
                    await _sessionStorage.SetAsync("selectedFacility", _selectedFacility.Facility);
                    await _sessionStorage.SetAsync("selectedCommunityKey", _selectedFacility.CommunityKey ?? string.Empty);
                }
                else if (_adFacilities.Count > 1)
                {
                    var savedFacility = await _sessionStorage.GetAsync<string>("selectedFacility");
                    var savedCommunityKey = await _sessionStorage.GetAsync<string>("selectedCommunityKey");

                    if (savedFacility.Success && !string.IsNullOrWhiteSpace(savedFacility.Value))
                    {
                        _selectedFacility = _adFacilities.FirstOrDefault(x =>
                            string.Equals(x.Facility, savedFacility.Value, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(x.CommunityKey ?? string.Empty, savedCommunityKey.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                    }
                }
            }
            catch (Exception ex)
            {
                await _logService.LogAsync($"Unable to load AD facilities: {ex.Message}", CurrentUser.Email);
            }

            OnAuthStateChanged?.Invoke();
        }

        public async Task SetSelectedFacilityAsync(string facility, string? communityKey)
        {
            if (_adFacilities.Count == 0)
            {
                return;
            }

            var selected = _adFacilities.FirstOrDefault(x =>
                string.Equals(x.Facility, facility, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.CommunityKey ?? string.Empty, communityKey ?? string.Empty, StringComparison.OrdinalIgnoreCase));

            if (selected == null)
            {
                return;
            }

            _selectedFacility = selected;
            await _sessionStorage.SetAsync("selectedFacility", selected.Facility);
            await _sessionStorage.SetAsync("selectedCommunityKey", selected.CommunityKey ?? string.Empty);
            OnAuthStateChanged?.Invoke();
        }

        public async Task RefreshCurrentUserAsync()
        {
            if (CurrentUser == null)
            {
                return;
            }

            User? dbUser = null;
            if (!string.IsNullOrWhiteSpace(CurrentUser.Email))
            {
                dbUser = await _authService.GetUserByEmailAsync(CurrentUser.Email);
            }

            if (dbUser == null && !string.IsNullOrWhiteSpace(CurrentUser.SAMAccountName))
            {
                dbUser = await _authService.GetUserBySamAccountNameAsync(CurrentUser.SAMAccountName);
            }

            if (dbUser != null)
            {
                await SetUserAsync(dbUser);
                await LoadADFacilitiesAsync();
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
            _selectedFacility = null;
            _adFacilities = new List<ADFacilityUser>();
            await _sessionStorage.DeleteAsync("currentUser");
            await _sessionStorage.DeleteAsync("selectedFacility");
            await _sessionStorage.DeleteAsync("selectedCommunityKey");
        }
    }
}