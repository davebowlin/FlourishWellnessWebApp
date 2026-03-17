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
builder.Services.AddScoped<ADFacilityService>();

// Database context factory
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(defaultConnection));

builder.Services.AddScoped<AuthStateService>();
builder.Services.AddScoped<SurveyService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<LogService>();

var app = builder.Build();

// 4. Do not apply startup schema/table creation logic.

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