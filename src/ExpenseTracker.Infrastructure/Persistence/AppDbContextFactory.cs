using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ExpenseTracker.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c> at design time.
/// <para>
/// Without this, the tooling would build the context from the API host and inherit
/// whatever provider is configured — including the in-memory one, which cannot
/// produce a migration. Pinning SQL Server here means <c>dotnet ef migrations add</c>
/// works the same way regardless of local settings.
/// </para>
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string FallbackConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=ExpenseTracker;Trusted_Connection=True;TrustServerCertificate=True";

    public AppDbContext CreateDbContext(string[] args)
    {
        // Migrations are generated from the model, not from a live database, so this
        // connection string only has to parse.
        var connectionString =
            Environment.GetEnvironmentVariable("EXPENSETRACKER_DESIGNTIME_CONNECTION")
            ?? FallbackConnectionString;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
