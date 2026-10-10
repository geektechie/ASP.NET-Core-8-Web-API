using Microsoft.EntityFrameworkCore;
using ExpenseTracker.Infrastructure.Persistence;

namespace ExpenseTracker.Api.Extensions;

public static class DatabaseInitializerExtensions
{
    /// <summary>
    /// Brings the local database up to the latest migration and seeds starter
    /// categories.
    /// <para>
    /// Called only outside production. In production, migrations belong in the deploy
    /// pipeline: running them from app startup means every instance in a rolling
    /// deployment races to apply the same schema change.
    /// </para>
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<WebApplication>>();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync();
            logger.LogInformation("Database schema is up to date.");
        }
        else
        {
            logger.LogWarning("Running on the in-memory provider; data will not survive a restart.");
        }

        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
    }
}
