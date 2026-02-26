using FlourishWellness.Components;
using FlourishWellness.Data;
using FlourishWellness.Services;
using FlourishWellness.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Negotiate;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(defaultConnection))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is missing or empty. Set it in appsettings.json or appsettings.Development.json before starting the app.");
}

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
    options.UseSqlServer(defaultConnection));

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
        context.Database.ExecuteSqlRaw("ALTER TABLE [Users] ADD [FullName] NVARCHAR(256) NOT NULL CONSTRAINT [DF_Users_FullName] DEFAULT '';");
    }

    if (!HasColumn(connection, "Sections", "SurveyEntityId"))
    {
        context.Database.ExecuteSqlRaw("ALTER TABLE [Sections] ADD [SurveyEntityId] INT NOT NULL CONSTRAINT [DF_Sections_SurveyEntityId] DEFAULT 0;");
    }

    if (!HasColumn(connection, "Questions", "SurveyEntityId"))
    {
        context.Database.ExecuteSqlRaw("ALTER TABLE [Questions] ADD [SurveyEntityId] INT NOT NULL CONSTRAINT [DF_Questions_SurveyEntityId] DEFAULT 0;");
    }

    if (!HasColumn(connection, "Responses", "SurveyEntityId"))
    {
        context.Database.ExecuteSqlRaw("ALTER TABLE [Responses] ADD [SurveyEntityId] INT NOT NULL CONSTRAINT [DF_Responses_SurveyEntityId] DEFAULT 0;");
    }

    context.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.SurveyEntities', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SurveyEntities] (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_SurveyEntities] PRIMARY KEY,
        [Year] INT NOT NULL,
        [Status] INT NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL
    );
END;");

    context.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.UserSurveyStatuses', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UserSurveyStatuses] (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_UserSurveyStatuses] PRIMARY KEY,
        [UserId] INT NOT NULL,
        [SurveyEntityId] INT NOT NULL,
        [IsCompleted] BIT NOT NULL,
        [UpdatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [FK_UserSurveyStatuses_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserSurveyStatuses_SurveyEntities_SurveyEntityId] FOREIGN KEY ([SurveyEntityId]) REFERENCES [dbo].[SurveyEntities]([Id]) ON DELETE CASCADE
    );
END;");

    context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SurveyEntities_Year' AND object_id = OBJECT_ID(N'dbo.SurveyEntities'))
BEGIN
    CREATE UNIQUE INDEX [IX_SurveyEntities_Year] ON [dbo].[SurveyEntities]([Year]);
END;");

    context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserSurveyStatuses_UserId_SurveyEntityId' AND object_id = OBJECT_ID(N'dbo.UserSurveyStatuses'))
BEGIN
    CREATE UNIQUE INDEX [IX_UserSurveyStatuses_UserId_SurveyEntityId] ON [dbo].[UserSurveyStatuses]([UserId], [SurveyEntityId]);
END;");

    context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sections_SurveyEntityId' AND object_id = OBJECT_ID(N'dbo.Sections'))
BEGIN
    CREATE INDEX [IX_Sections_SurveyEntityId] ON [dbo].[Sections]([SurveyEntityId]);
END;");

    context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Questions_SurveyEntityId' AND object_id = OBJECT_ID(N'dbo.Questions'))
BEGIN
    CREATE INDEX [IX_Questions_SurveyEntityId] ON [dbo].[Questions]([SurveyEntityId]);
END;");

    context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Responses_SurveyEntityId' AND object_id = OBJECT_ID(N'dbo.Responses'))
BEGIN
    CREATE INDEX [IX_Responses_SurveyEntityId] ON [dbo].[Responses]([SurveyEntityId]);
END;");

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
    command.CommandText = @"
SELECT COUNT(1)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @tableName AND COLUMN_NAME = @columnName;";

    var tableNameParameter = command.CreateParameter();
    tableNameParameter.ParameterName = "@tableName";
    tableNameParameter.Value = tableName;
    command.Parameters.Add(tableNameParameter);

    var columnNameParameter = command.CreateParameter();
    columnNameParameter.ParameterName = "@columnName";
    columnNameParameter.Value = columnName;
    command.Parameters.Add(columnNameParameter);

    var result = command.ExecuteScalar();
    return result != null && Convert.ToInt32(result) > 0;
}