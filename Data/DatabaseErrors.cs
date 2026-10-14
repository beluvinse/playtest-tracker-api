using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace PlaytestTracker.Api.Data
{
    // Telling apart the errors the database can answer with. They are database-specific: this is
    // the only place that knows PostgreSQL's way of saying it.
    public static class DatabaseErrors
    {
        // "That value already exists": a unique index (or primary key) refused a duplicate.
        // PostgreSQL's code for it is 23505 ("unique_violation"). The checks the services do
        // before saving catch the usual case; this one is for two requests racing each other.
        public static bool IsDuplicateKey(this DbUpdateException exception)
            => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }
}
