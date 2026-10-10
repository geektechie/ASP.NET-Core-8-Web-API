using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Infrastructure.Persistence;
using ExpenseTracker.Infrastructure.Persistence.Queries;
using ExpenseTracker.Infrastructure.Persistence.Repositories;
using ExpenseTracker.Infrastructure.Services;

namespace ExpenseTracker.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Set <c>Database:Provider</c> to this to run with no database installed.</summary>
    private const string InMemoryProvider = "InMemory";

    private const string InMemoryDatabaseName = "ExpenseTracker";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options => ConfigureProvider(options, configuration));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IBudgetRepository, BudgetRepository>();
        services.AddScoped<IReportQueries, ReportQueries>();
        services.AddScoped<DatabaseSeeder>();

        // Stateless and thread-safe, so one instance is enough.
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        return services;
    }

    public static bool IsUsingInMemoryDatabase(IConfiguration configuration) =>
        string.Equals(configuration["Database:Provider"], InMemoryProvider, StringComparison.OrdinalIgnoreCase)
        || string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection"));

    private static void ConfigureProvider(DbContextOptionsBuilder options, IConfiguration configuration)
    {
        if (IsUsingInMemoryDatabase(configuration))
        {
            // Lets the project be cloned and run with no SQL Server at all. The
            // in-memory provider does not enforce unique indexes or foreign keys, so
            // it is for local experimentation and tests, never for production.
            options.UseInMemoryDatabase(InMemoryDatabaseName);
            return;
        }

        options.UseSqlServer(
            configuration.GetConnectionString("DefaultConnection"),
            sqlServer => sqlServer.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null));
    }
}
