using Microsoft.EntityFrameworkCore;
using ExpenseTracker.Application.DTOs;
using ExpenseTracker.Application.Options;
using ExpenseTracker.Application.Services;
using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Infrastructure.Persistence;
using ExpenseTracker.Infrastructure.Persistence.Queries;
using ExpenseTracker.Infrastructure.Persistence.Repositories;

namespace ExpenseTracker.UnitTests.Support;

/// <summary>
/// Wires the real services over a throwaway in-memory database, so a test exercises
/// the same repository and query code the API runs rather than a stand-in for it.
/// </summary>
public sealed class TrackerFixture : IDisposable
{
    public TrackerFixture(Action<TrackerOptions>? configure = null)
    {
        Options = new TrackerOptions
        {
            Currency = "USD",
            DefaultReportDays = 30,

            // Tests arrange the categories they need; the starter set would only add
            // rows every assertion has to work around.
            SeedDefaultCategories = false
        };

        configure?.Invoke(Options);

        var wrapped = Microsoft.Extensions.Options.Options.Create(Options);

        // A fresh database name per fixture keeps tests from seeing each other's rows.
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"expensetracker-tests-{Guid.NewGuid()}")
            .Options;

        Db = new AppDbContext(dbOptions);
        Clock = new FakeDateTimeProvider();

        var unitOfWork = new UnitOfWork(Db);
        var categoryRepository = new CategoryRepository(Db);
        var expenseRepository = new ExpenseRepository(Db);
        var budgetRepository = new BudgetRepository(Db);

        Queries = new ReportQueries(Db);

        Categories = new CategoryService(categoryRepository, unitOfWork, Clock);
        Expenses = new ExpenseService(expenseRepository, categoryRepository, unitOfWork, Clock, wrapped);
        Budgets = new BudgetService(budgetRepository, categoryRepository, unitOfWork, Clock, wrapped);
        Reports = new ReportService(Queries, Clock, wrapped);
    }

    public TrackerOptions Options { get; }

    public AppDbContext Db { get; }

    public FakeDateTimeProvider Clock { get; }

    public ReportQueries Queries { get; }

    public CategoryService Categories { get; }

    public ExpenseService Expenses { get; }

    public BudgetService Budgets { get; }

    public ReportService Reports { get; }

    public Task<CategoryResponse> AddCategoryAsync(string name, string colorHex = "#2E7D32") =>
        Categories.CreateAsync(new CreateCategoryRequest { Name = name, ColorHex = colorHex });

    public Task<ExpenseResponse> AddExpenseAsync(
        decimal amount,
        DateOnly spentOn,
        Guid categoryId,
        string description = "Test expense",
        PaymentMethod paymentMethod = PaymentMethod.Cash) =>
        Expenses.CreateAsync(new CreateExpenseRequest
        {
            Amount = amount,
            SpentOn = spentOn,
            CategoryId = categoryId,
            Description = description,
            PaymentMethod = paymentMethod
        });

    public Task<BudgetResponse> SetBudgetAsync(Guid categoryId, string month, decimal limit) =>
        Budgets.UpsertAsync(new UpsertBudgetRequest
        {
            CategoryId = categoryId,
            Month = month,
            Limit = limit
        });

    public void Dispose() => Db.Dispose();
}
