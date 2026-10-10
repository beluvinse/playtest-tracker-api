using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PlaytestTracker.Api.Data;

namespace PlaytestTracker.Api.Tests.Support;

// Starts the real API (Program.cs, controllers, authentication, everything) inside the test,
// with an HttpClient wired straight to it: no network and no port. The only differences from
// the real app are the database (SQLite in memory instead of SQL Server) and the settings.
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly string _jwtKey;
    private readonly TimeProvider? _clock;

    public ApiFactory(string jwtKey = TestApp.JwtKey, TimeProvider? clock = null)
    {
        _jwtKey = jwtKey;
        _clock = clock;
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development": that one would load the user-secrets of whoever runs the tests
        builder.UseEnvironment("Testing");

        // Without this, the test output fills up with every SQL command EF runs ("info: ...").
        // Warnings and errors still show, which is what matters when a test fails.
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = TestApp.JwtIssuer,
                ["Jwt:Audience"] = TestApp.JwtAudience,
                ["Jwt:ExpiresMinutes"] = "60",
                ["Jwt:Key"] = _jwtKey
            }));

        builder.ConfigureServices(services =>
        {
            // Swap SQL Server for the in-memory database
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            if (_clock != null)
                services.AddSingleton(_clock);
        });
    }

    // Creates the (empty) tables. Call it once, right after creating the factory.
    public ApiFactory WithEmptyDatabase()
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        return this;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
