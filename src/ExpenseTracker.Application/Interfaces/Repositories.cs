using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.DTOs;
using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.ValueObjects;

namespace ExpenseTracker.Application.Interfaces;

/// <summary>
/// Saving is separated from the repositories so one use case can touch several
/// aggregates and still commit once. Without it, each repository would own its own
/// <c>SaveChanges</c> and a half-applied operation would be the normal case.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the last save failed on a unique index. Lets a service turn a race
    /// into a clean 409 without leaking provider-specific exception types upward.
    /// </summary>
    bool IsUniqueViolation(Exception exception);
}

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Category>> ListAsync(bool includeArchived, CancellationToken cancellationToken = default);

    /// <summary>Case-insensitive, so "Groceries" and "groceries" cannot both exist.</summary>
    Task<bool> NameExistsAsync(string name, Guid? excludingId = null, CancellationToken cancellationToken = default);

    Task<int> CountExpensesAsync(Guid categoryId, CancellationToken cancellationToken = default);

    /// <summary>Expense counts for several categories at once, keyed by category id.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountExpensesByCategoryAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Category category, CancellationToken cancellationToken = default);

    void Remove(Category category);
}

public interface IExpenseRepository
{
    /// <summary>Tracked load for update paths.</summary>
    Task<Expense?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Filtered, sorted and paged, with each expense's category joined in.</summary>
    Task<PagedResult<ExpenseWithCategory>> QueryAsync(
        ExpenseQuery query,
        CancellationToken cancellationToken = default);

    Task<ExpenseWithCategory?> GetWithCategoryAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Expense expense, CancellationToken cancellationToken = default);

    void Remove(Expense expense);
}

public interface IBudgetRepository
{
    Task<Budget?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Budget?> GetForAsync(Guid categoryId, BudgetMonth month, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BudgetWithCategory>> ListForMonthAsync(
        BudgetMonth month,
        CancellationToken cancellationToken = default);

    Task<BudgetWithCategory?> GetWithCategoryAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Budget budget, CancellationToken cancellationToken = default);

    void Remove(Budget budget);
}

/// <summary>An expense plus the category fields a response needs, fetched in one query.</summary>
public sealed record ExpenseWithCategory(Expense Expense, string CategoryName, string CategoryColorHex);

/// <summary>A budget plus the category fields a response needs, fetched in one query.</summary>
public sealed record BudgetWithCategory(Budget Budget, string CategoryName, string CategoryColorHex);
