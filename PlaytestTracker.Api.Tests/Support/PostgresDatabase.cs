using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using PlaytestTracker.Api.Data;

namespace PlaytestTracker.Api.Tests.Support;

// A brand-new, empty PostgreSQL database, with the migrations applied, for one test. When the test
// ends the database is dropped, so no test can leave anything behind for another one.
//
// Most tests of this project run on SQLite in memory (fast, nothing to install). SQLite is not
// PostgreSQL, though, and some things only the real one can answer: do the migrations apply? does
// the database refuse a duplicate the way the code expects? does text search ignore capitals?
// Those tests use this class. They need a PostgreSQL server to be reachable: its connection string
// goes in the PLAYTEST_TEST_POSTGRES environment variable (a server, no database name needed).
// Without it they are skipped, so `dotnet test` still works on a computer without Docker.
public sealed class PostgresDatabase : IDisposable
{
    public const string EnvironmentVariable = "PLAYTEST_TEST_POSTGRES";

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable));

    private readonly NpgsqlConnectionStringBuilder _server;
    private readonly string _name = "playtest_test_" + Guid.NewGuid().ToString("N");

    public PostgresDatabase()
    {
        _server = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(EnvironmentVariable));
        ConnectionString = new NpgsqlConnectionStringBuilder(_server.ConnectionString) { Database = _name }
            .ConnectionString;

        Run($"CREATE DATABASE \"{_name}\"");

        // The same thing `dotnet ef database update` does: the real migrations, on an empty database
        using var context = NewContext();
        context.Database.Migrate();
    }

    public string ConnectionString { get; }

    // A new DbContext on this database. Each one has its own connection, like each request of the
    // API does, so two of them can work "at the same time".
    public AppDbContext NewContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(interceptors)
            .Options;
        return new AppDbContext(options);
    }

    public void Dispose()
    {
        // Connections are kept in a pool for reuse; close them, or the database can't be dropped
        NpgsqlConnection.ClearAllPools();
        Run($"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)");
    }

    // Runs one command on the server itself ("postgres" is the database every server has)
    private void Run(string sql)
    {
        var admin = new NpgsqlConnectionStringBuilder(_server.ConnectionString) { Database = "postgres" };
        using var connection = new NpgsqlConnection(admin.ConnectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }
}

// Runs a piece of code right BEFORE the context saves, once. It is how a test makes "somebody else
// got in first" happen at exactly the right moment, which can't be done by luck with real requests.
public sealed class BeforeFirstSave : SaveChangesInterceptor
{
    private readonly Action _action;
    private bool _done;

    public BeforeFirstSave(Action action) => _action = action;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (!_done)
        {
            _done = true;
            _action();
        }
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}

// [PostgresFact] is a [Fact] that only runs when a PostgreSQL server is configured
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (!PostgresDatabase.IsConfigured)
            Skip = $"Needs a PostgreSQL server: set {PostgresDatabase.EnvironmentVariable} (see the README).";
    }
}
