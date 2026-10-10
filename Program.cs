using System.Text.Json.Serialization;
using PlaytestTracker.Api.Services;
using Microsoft.EntityFrameworkCore;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity: users, password hashing and lockout. AddIdentityCore is the small version
// (no cookies and no built-in pages), because this API will sign people in with tokens.
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        // Length matters more than symbols: a long password is hard to guess, and easier to remember
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;

        // The email is how a person is identified, so two accounts can't share one
        options.User.RequireUniqueEmail = true;

        // 5 wrong passwords in a row lock the account for 10 minutes (slows down guessing)
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<AppDbContext>();

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

app.UseAuthorization();

app.MapControllers();

app.Run();
