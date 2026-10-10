using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Options;
using ExpenseTracker.Application.Services;

namespace ExpenseTracker.Application;

/// <summary>
/// Each layer registers its own services, so <c>Program.cs</c> never has to know what
/// lives inside one.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<TrackerOptions>()
            .Bind(configuration.GetSection(TrackerOptions.SectionName))
            .ValidateDataAnnotations();

        // ValidateOnStart lives in Microsoft.Extensions.Hosting and is applied by the
        // API host instead, which keeps a hosting dependency out of this layer.

        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
