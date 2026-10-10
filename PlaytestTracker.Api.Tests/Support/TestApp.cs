using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.Extensions;
using PlaytestTracker.Api.Services;

namespace PlaytestTracker.Api.Tests.Support;

// A small version of the real app for tests: the same services and the same Identity rules,
// but on a brand-new, empty database that lives only in memory (SQLite). Every test creates
// its own, so no test can leave data behind for another one.
public sealed class TestApp : IDisposable
{
    // An in-memory SQLite database exists only while its connection is open
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    public TestApp()
    {
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        services.AddAppIdentity(); // the same rules the API uses
        services.AddScoped<AuthService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<BugService>();

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();

        // Builds the tables from the model (the migrations contain SQL Server-only SQL)
        Get<AppDbContext>().Database.EnsureCreated();
    }

    public T Get<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        _connection.Dispose();
    }
}
