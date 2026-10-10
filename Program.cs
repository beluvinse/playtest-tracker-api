using System.Text.Json.Serialization;
using PlaytestTracker.Api.Services;
using Microsoft.EntityFrameworkCore;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Users, password hashing and lockout (the rules live in IdentityServiceExtensions)
builder.Services.AddAppIdentity();

// Tokens: how the API recognizes a person on each request (the settings live in "Jwt")
builder.Services.AddAppAuthentication();

builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<BugService>();
builder.Services.AddScoped<AuthService>();

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

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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
