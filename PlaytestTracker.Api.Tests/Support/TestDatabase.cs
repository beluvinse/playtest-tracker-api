using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PlaytestTracker.Api.Tests.Support;

// The database the tests use: SQLite in memory, instead of SQL Server
public static class TestDatabase
{
    public static void UseSqliteInMemory(DbContextOptionsBuilder options, SqliteConnection connection)
        => options
            .UseSqlite(connection)
            .ReplaceService<IModelCustomizer, SortableDates>();

    // SQLite can't sort by a DateTimeOffset column ("ORDER BY CreatedAt" fails), and the API
    // does that all the time (newest first). Here those dates are saved as numbers, which SQLite
    // sorts fine. It only affects the tests: the real API uses SQL Server, which has no problem.
    private sealed class SortableDates : ModelCustomizer
    {
        public SortableDates(ModelCustomizerDependencies dependencies) : base(dependencies) { }

        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);

            var dates = modelBuilder.Model.GetEntityTypes()
                .SelectMany(entity => entity.GetProperties())
                .Where(property => property.ClrType == typeof(DateTimeOffset));

            foreach (var property in dates)
                property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
        }
    }
}
