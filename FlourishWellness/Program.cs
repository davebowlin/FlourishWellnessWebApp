using FlourishWellness.Components;
using FlourishWellness.Data;
using FlourishWellness.Services;
using FlourishWellness.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Negotiate;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Razor Components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 2. RE-ADD Windows Authentication Services
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;
});
builder.Services.AddCascadingAuthenticationState();

// 3. Register Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ADUserService>();

// Database context factory
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<AuthStateService>();
builder.Services.AddScoped<SurveyService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<LogService>();

var app = builder.Build();

// 4. Initialize/Migrate Database
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    using var context = factory.CreateDbContext();
    context.Database.EnsureCreated();

    var connection = context.Database.GetDbConnection();
    if (connection.State != ConnectionState.Open)
    {
        connection.Open();
    }

    if (!HasColumn(connection, "Users", "FullName"))
    {
        context.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN FullName TEXT NOT NULL DEFAULT '';");
    }

    if (!HasColumn(connection, "Sections", "SurveyEntityId"))
    {
        context.Database.ExecuteSqlRaw("ALTER TABLE Sections ADD COLUMN SurveyEntityId INTEGER NOT NULL DEFAULT 0;");
    }

    if (!HasColumn(connection, "Questions", "SurveyEntityId"))
    {
        context.Database.ExecuteSqlRaw("ALTER TABLE Questions ADD COLUMN SurveyEntityId INTEGER NOT NULL DEFAULT 0;");
    }

    if (!HasColumn(connection, "Responses", "SurveyEntityId"))
    {
        context.Database.ExecuteSqlRaw("ALTER TABLE Responses ADD COLUMN SurveyEntityId INTEGER NOT NULL DEFAULT 0;");
    }

    context.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS SurveyEntities (
    Id INTEGER NOT NULL CONSTRAINT PK_SurveyEntities PRIMARY KEY AUTOINCREMENT,
    Year INTEGER NOT NULL,
    Status INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL
);");

    context.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS UserSurveyStatuses (
    Id INTEGER NOT NULL CONSTRAINT PK_UserSurveyStatuses PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER NOT NULL,
    SurveyEntityId INTEGER NOT NULL,
    IsCompleted INTEGER NOT NULL,
    UpdatedAt TEXT NOT NULL,
    CONSTRAINT FK_UserSurveyStatuses_Users_UserId FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserSurveyStatuses_SurveyEntities_SurveyEntityId FOREIGN KEY(SurveyEntityId) REFERENCES SurveyEntities(Id) ON DELETE CASCADE
);");

    context.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_SurveyEntities_Year ON SurveyEntities (Year);");
    context.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_UserSurveyStatuses_UserId_SurveyEntityId ON UserSurveyStatuses (UserId, SurveyEntityId);");
    context.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_Sections_SurveyEntityId ON Sections (SurveyEntityId);");
    context.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_Questions_SurveyEntityId ON Questions (SurveyEntityId);");
    context.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_Responses_SurveyEntityId ON Responses (SurveyEntityId);");

    var activeEntity = context.SurveyEntities.FirstOrDefault(e => e.Status == SurveyEntityStatus.Active);
    if (activeEntity == null)
    {
        var currentYear = DateTime.UtcNow.Year;
        activeEntity = context.SurveyEntities.FirstOrDefault(e => e.Year == currentYear);
        if (activeEntity == null)
        {
            activeEntity = new SurveyEntity
            {
                Year = currentYear,
                Status = SurveyEntityStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            context.SurveyEntities.Add(activeEntity);
            context.SaveChanges();
        }
        else
        {
            activeEntity.Status = SurveyEntityStatus.Active;
            context.SaveChanges();
        }
    }

    var allOtherEntities = context.SurveyEntities.Where(e => e.Id != activeEntity.Id && e.Status == SurveyEntityStatus.Active).ToList();
    if (allOtherEntities.Any())
    {
        foreach (var entity in allOtherEntities)
        {
            entity.Status = SurveyEntityStatus.Archived;
        }
        context.SaveChanges();
    }

    context.Database.ExecuteSql($"UPDATE Sections SET SurveyEntityId = {activeEntity.Id} WHERE SurveyEntityId = 0;");
    context.Database.ExecuteSqlRaw(@"
UPDATE Questions
SET SurveyEntityId = (
    SELECT s.SurveyEntityId
    FROM Sections s
    WHERE s.Id = Questions.SectionId
)
WHERE SurveyEntityId = 0;");

    context.Database.ExecuteSqlRaw(@"
UPDATE Responses
SET SurveyEntityId = (
    SELECT q.SurveyEntityId
    FROM Questions q
    WHERE q.Id = Responses.QuestionId
)
WHERE SurveyEntityId = 0;");

    var completedUsers = context.Users.Where(u => u.IsSurveyCompleted).ToList();
    foreach (var user in completedUsers)
    {
        var existingStatus = context.UserSurveyStatuses
            .FirstOrDefault(s => s.UserId == user.Id && s.SurveyEntityId == activeEntity.Id);

        if (existingStatus == null)
        {
            context.UserSurveyStatuses.Add(new UserSurveyStatus
            {
                UserId = user.Id,
                SurveyEntityId = activeEntity.Id,
                IsCompleted = true,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else if (!existingStatus.IsCompleted)
        {
            existingStatus.IsCompleted = true;
            existingStatus.UpdatedAt = DateTime.UtcNow;
        }
    }
    context.SaveChanges();

    var blankSections = context.Sections.Where(s => string.IsNullOrWhiteSpace(s.Name)).ToList();
    if (blankSections.Any())
    {
        context.Sections.RemoveRange(blankSections);
        context.SaveChanges();
    }

    var adminUser = context.Users.FirstOrDefault(u => u.Email.ToLower() == "admin");
    if (adminUser == null)
    {
        context.Users.Add(new User
        {
            Email = "admin",
            FullName = "Administrator",
            PasswordHash = "admin",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        });
        context.SaveChanges();
    }
    else
    {
        adminUser.Role = UserRole.Admin;
        if (string.IsNullOrWhiteSpace(adminUser.FullName))
        {
            adminUser.FullName = "Administrator";
        }
        if (string.IsNullOrWhiteSpace(adminUser.PasswordHash))
        {
            adminUser.PasswordHash = "admin";
        }
        context.SaveChanges();
    }
}

// 5. Middleware Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 6. RE-ADD Auth Middleware so the app can read the AD User
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static bool HasColumn(System.Data.Common.DbConnection connection, string tableName, string columnName)
{
    using var command = connection.CreateCommand();
    command.CommandText = $"PRAGMA table_info('{tableName}')";
    using var reader = command.ExecuteReader();

    while (reader.Read())
    {
        var existingColumn = reader["name"]?.ToString();
        if (string.Equals(existingColumn, columnName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}