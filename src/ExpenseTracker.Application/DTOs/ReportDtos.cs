using ExpenseTracker.Domain.Enums;

namespace ExpenseTracker.Application.DTOs;

/// <summary>Bucket size for a trend series.</summary>
public enum TrendGranularity
{
    Daily = 0,
    Monthly = 1
}

/// <summary>
/// Totals for a date range, broken down two ways.
/// </summary>
/// <param name="DailyAverage">Total divided by every day in the range, including days with no spending.</param>
public sealed record SpendingSummaryResponse(
    DateOnly From,
    DateOnly To,
    int DayCount,
    string Currency,
    decimal Total,
    int ExpenseCount,
    decimal DailyAverage,
    decimal LargestExpense,
    IReadOnlyList<CategoryBreakdownItem> ByCategory,
    IReadOnlyList<PaymentMethodBreakdownItem> ByPaymentMethod);

/// <param name="PercentOfTotal">Share of the range's total, 0–100, rounded to one decimal.</param>
public sealed record CategoryBreakdownItem(
    Guid CategoryId,
    string CategoryName,
    string ColorHex,
    decimal Total,
    int Count,
    decimal PercentOfTotal);

public sealed record PaymentMethodBreakdownItem(
    PaymentMethod PaymentMethod,
    decimal Total,
    int Count,
    decimal PercentOfTotal);

/// <summary>
/// A spending series over time.
/// </summary>
/// <param name="Points">
/// Every bucket in the range, including empty ones. A chart needs a point for each
/// period to draw a continuous axis, and zero-filling here saves every client from
/// reimplementing it.
/// </param>
public sealed record SpendingTrendResponse(
    TrendGranularity Granularity,
    DateOnly From,
    DateOnly To,
    string Currency,
    decimal Total,
    IReadOnlyList<TrendPoint> Points);

/// <param name="Label">Pre-formatted axis label: <c>2026-10-09</c> daily, <c>2026-10</c> monthly.</param>
public sealed record TrendPoint(
    DateOnly PeriodStart,
    string Label,
    decimal Total,
    int Count);

/// <summary>
/// Budget versus actual for one month.
/// </summary>
/// <param name="DaysElapsed">Days of the month already gone, used for the projection. Equals <c>DaysInMonth</c> for a past month.</param>
public sealed record BudgetStatusResponse(
    string Month,
    string Currency,
    decimal TotalLimit,
    decimal TotalSpent,
    decimal TotalRemaining,
    int DaysInMonth,
    int DaysElapsed,
    bool IsCurrentMonth,
    IReadOnlyList<BudgetStatusItem> Categories);

/// <summary>
/// One category's standing for the month. Covers categories with a budget, with
/// spending, or with both — so unbudgeted spending cannot hide from the report.
/// </summary>
/// <param name="Limit">Null when the category has spending but no budget set.</param>
/// <param name="ProjectedSpend">
/// Spend extrapolated to the end of the month at the current daily rate. Only
/// populated for the month in progress; null for a month already finished.
/// </param>
public sealed record BudgetStatusItem(
    Guid CategoryId,
    string CategoryName,
    string ColorHex,
    decimal? Limit,
    decimal Spent,
    decimal? Remaining,
    decimal? PercentUsed,
    bool IsOverBudget,
    decimal? ProjectedSpend);
