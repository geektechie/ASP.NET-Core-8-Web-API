using ExpenseTracker.Domain.Exceptions;
using ExpenseTracker.Domain.ValueObjects;

namespace ExpenseTracker.Domain.Entities;

/// <summary>
/// A spending limit for one category in one month.
/// <para>
/// Per month rather than open-ended, because that is how people actually budget, and
/// it means raising next month's grocery limit never rewrites what last month was
/// measured against.
/// </para>
/// </summary>
public sealed class Budget
{
    /// <summary>Required by EF Core for materialisation; not for application use.</summary>
    private Budget()
    {
    }

    private Budget(Guid categoryId, BudgetMonth month, Money limit, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        CategoryId = categoryId;
        Month = month;
        Limit = limit.Amount;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid CategoryId { get; private set; }

    public BudgetMonth Month { get; private set; }

    /// <summary>
    /// A plain decimal for the same reason as <see cref="Expense.Amount"/>: writes are
    /// guarded by <see cref="Money"/>, while the column stays summable in SQL.
    /// </summary>
    public decimal Limit { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Budget Create(Category category, BudgetMonth month, Money limit, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(category);

        // An archived category can still hold a budget for a month already past —
        // what it cannot do is take new spending. That rule lives on Expense.
        return new Budget(category.Id, month, EnsurePositive(limit), now);
    }

    public void ChangeLimit(Money limit, DateTimeOffset now)
    {
        Limit = EnsurePositive(limit).Amount;
        UpdatedAt = now;
    }

    private static Money EnsurePositive(Money limit) =>
        limit.IsPositive
            ? limit
            : throw new InvalidAmountException("A budget limit must be greater than zero.");
}
