using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.DTOs;

namespace ExpenseTracker.Application.Interfaces;

/// <summary>The clock, behind an interface so date rules can be tested without waiting.</summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }

    /// <summary>Today as a calendar date, which is what expenses are dated by.</summary>
    DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryResponse>> ListAsync(
        bool includeArchived,
        CancellationToken cancellationToken = default);

    Task<CategoryResponse> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CategoryResponse> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task<CategoryResponse> UpdateAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Refused while any expense still references the category; archive it instead.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IExpenseService
{
    Task<PagedResult<ExpenseResponse>> QueryAsync(
        ExpenseQuery query,
        CancellationToken cancellationToken = default);

    Task<ExpenseResponse> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ExpenseResponse> CreateAsync(
        CreateExpenseRequest request,
        CancellationToken cancellationToken = default);

    Task<ExpenseResponse> UpdateAsync(
        Guid id,
        UpdateExpenseRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IBudgetService
{
    Task<IReadOnlyList<BudgetResponse>> ListForMonthAsync(
        string month,
        CancellationToken cancellationToken = default);

    /// <summary>Creates or updates the single budget for the request's category and month.</summary>
    Task<BudgetResponse> UpsertAsync(
        UpsertBudgetRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IReportService
{
    /// <summary>Defaults to the configured window ending today when dates are omitted.</summary>
    Task<SpendingSummaryResponse> GetSummaryAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default);

    Task<SpendingTrendResponse> GetTrendAsync(
        DateOnly? from,
        DateOnly? to,
        TrendGranularity granularity,
        CancellationToken cancellationToken = default);

    /// <summary>Defaults to the current month when none is given.</summary>
    Task<BudgetStatusResponse> GetBudgetStatusAsync(
        string? month,
        CancellationToken cancellationToken = default);
}
