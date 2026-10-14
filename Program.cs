using System.Text.Json.Serialization;
using PlaytestTracker.Api.Services;
using Microsoft.EntityFrameworkCore;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.Extensions;
using PlaytestTracker.Api.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// PostgreSQL. The connection string comes from configuration: appsettings.Development.json when
// developing, and the ConnectionStrings__DefaultConnection environment variable anywhere else
// (it holds the password, so it never lives in a file of the repo outside of development)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "The database connection string is missing. Set ConnectionStrings:DefaultConnection " +
            "(the ConnectionStrings__DefaultConnection environment variable).")));

// Users, password hashing and lockout (the rules live in IdentityServiceExtensions)
builder.Services.AddAppIdentity();

// Tokens: how the API recognizes a person on each request (the settings live in "Jwt")
builder.Services.AddAppAuthentication();

builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<BugService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<OrganizationService>();
builder.Services.AddScoped<MembershipService>();

// The example organization (the settings live in "Demo")
builder.Services.Configure<DemoOptions>(builder.Configuration.GetSection(DemoOptions.SectionName));
builder.Services.AddScoped<DemoSeeder>();
builder.Services.AddScoped<DemoService>();

// Browsers block calls from another origin (like the frontend dev server) unless the API allows it.
// Only the origins listed in configuration are allowed; with none listed, no other origin can call the API.
const string FrontendCorsPolicy = "Frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            // Lets the frontend read where a newly created resource lives (201 Created)
            .WithExposedHeaders("Location")));

// Swagger UI, with a button to paste the login token (see SwaggerServiceExtensions)
builder.Services.AddAppSwagger();

var app = builder.Build();

// Creates the demo organization if it doesn't exist yet. Safe to run on every start. If the
// database isn't ready (migrations not applied), the API still starts and says why in the log.
var demo = app.Configuration.GetSection(DemoOptions.SectionName).Get<DemoOptions>() ?? new DemoOptions();
if (demo.Enabled && demo.SeedOnStartup)
{
    using var scope = app.Services.CreateScope();
    try
    {
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedAsync();
    }
    catch (Exception exception)
    {
        app.Logger.LogError(exception,
            "Could not create the demo organization. Is the database up to date? (dotnet ef database update)");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(FrontendCorsPolicy);

// Order matters: first find out WHO the request is from (authentication),
// then decide what they are ALLOWED to do (authorization)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// With top-level statements, .NET generates the Program class as internal. Declaring it
// (empty) as public lets the tests start the whole API in memory.
public partial class Program { }
