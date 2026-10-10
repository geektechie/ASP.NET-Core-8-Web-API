using ExpenseTracker.Domain.Enums;

namespace ExpenseTracker.Application.Models;

/// <summary>
/// Raw aggregates as the database produces them.
/// <para>
/// Kept separate from the response DTOs so the query layer never has to know how the
/// public contract is shaped, and so percentages and gap-filling stay in one place in
/// the service rather than being half-computed in SQL.
/// </para>
/// </summary>
public sealed record SpendingTotals(decimal Total, int Count, decimal LargestExpense)
{
    public static readonly SpendingTotals Empty = new(0m, 0, 0m);
}

public sealed record CategoryTotal(
    Guid CategoryId,
    string CategoryName,
    string ColorHex,
    decimal Total,
    int Count);

public sealed record PaymentMethodTotal(PaymentMethod PaymentMethod, decimal Total, int Count);

/// <param name="PeriodStart">First day of the bucket: the day itself, or the first of the month.</param>
public sealed record PeriodTotal(DateOnly PeriodStart, decimal Total, int Count);

/// <summary>A category's budget and spend for one month, before the derived figures are worked out.</summary>
public sealed record CategoryBudgetActual(
    Guid CategoryId,
    string CategoryName,
    string ColorHex,
    decimal? Limit,
    decimal Spent);
