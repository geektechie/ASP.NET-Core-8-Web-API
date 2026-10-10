using Microsoft.Extensions.Options;
using ExpenseTracker.Application.DTOs;
using ExpenseTracker.Application.Exceptions;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using ExpenseTracker.Application.Options;
using ExpenseTracker.Domain.ValueObjects;

namespace ExpenseTracker.Application.Services;

/// <summary>
/// Turns database aggregates into report responses.
/// <para>
/// The database does the summing; this class does the arithmetic that depends on the
/// request — percentages, averages, gap-filling and projections. Keeping that split
/// means the SQL stays simple and the derived figures stay testable without one.
/// </para>
/// </summary>
public sealed class ReportService : IReportService
{
    private const int PercentDecimals = 1;
    private const int MoneyDecimals = 2;

    private readonly IReportQueries _queries;
    private readonly IDateTimeProvider _clock;
    private readonly TrackerOptions _options;

    public ReportService(
        IReportQueries queries,
        IDateTimeProvider clock,
        IOptions<TrackerOptions> options)
    {
        _queries = queries;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<SpendingSummaryResponse> GetSummaryAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var range = ResolveRange(from, to);

        var totals = await _queries.GetTotalsAsync(range, cancellationToken);
        var byCategory = await _queries.GetCategoryTotalsAsync(range, cancellationToken);
        var byPaymentMethod = await _queries.GetPaymentMethodTotalsAsync(range, cancellationToken);

        return new SpendingSummaryResponse(
            range.From,
            range.To,
            range.DayCount,
            _options.Currency,
            totals.Total,
            totals.Count,

            // Averaged over every day in the range, not only the days with spending:
            // "I spend 42 a day" is the question people are asking, and skipping the
            // quiet days would answer a different one.
            decimal.Round(totals.Total / range.DayCount, MoneyDecimals, MidpointRounding.AwayFromZero),
            totals.LargestExpense,
            ToBreakdown(byCategory, totals.Total),
            ToBreakdown(byPaymentMethod, totals.Total));
    }

    public async Task<SpendingTrendResponse> GetTrendAsync(
        DateOnly? from,
        DateOnly? to,
        TrendGranularity granularity,
        CancellationToken cancellationToken = default)
    {
        var range = ResolveRange(from, to);
        var monthly = granularity == TrendGranularity.Monthly;

        var totals = await _queries.GetPeriodTotalsAsync(range, monthly, cancellationToken);

        var points = monthly
            ? BuildMonthlyPoints(range, totals)
            : BuildDailyPoints(range, totals);

        return new SpendingTrendResponse(
            granularity,
            range.From,
            range.To,
            _options.Currency,
            points.Sum(point => point.Total),
            points);
    }

    public async Task<BudgetStatusResponse> GetBudgetStatusAsync(
        string? month,
        CancellationToken cancellationToken = default)
    {
        var budgetMonth = ResolveMonth(month);
        var today = _clock.Today;

        var daysInMonth = DateTime.DaysInMonth(budgetMonth.Year, budgetMonth.Month);
        var isCurrentMonth = budgetMonth == BudgetMonth.From(today);

        // A finished month is fully elapsed; a future month has not started. Only the
        // month in progress sits in between, and only it gets a projection.
        var daysElapsed = isCurrentMonth
            ? today.Day
            : budgetMonth.FirstDay > today ? 0 : daysInMonth;

        var actuals = await _queries.GetBudgetActualsAsync(budgetMonth, cancellationToken);

        var categories = actuals
            .Select(actual => ToStatusItem(actual, isCurrentMonth, daysElapsed, daysInMonth))
            .OrderByDescending(item => item.Spent)
            .ThenBy(item => item.CategoryName)
            .ToList();

        var totalLimit = actuals.Sum(actual => actual.Limit ?? 0m);
        var totalSpent = actuals.Sum(actual => actual.Spent);

        return new BudgetStatusResponse(
            budgetMonth.ToString(),
            _options.Currency,
            totalLimit,
            totalSpent,
            Math.Max(0m, totalLimit - totalSpent),
            daysInMonth,
            daysElapsed,
            isCurrentMonth,
            categories);
    }

    private BudgetStatusItem ToStatusItem(
        CategoryBudgetActual actual,
        bool isCurrentMonth,
        int daysElapsed,
        int daysInMonth)
    {
        decimal? remaining = actual.Limit is { } limit ? Math.Max(0m, limit - actual.Spent) : null;

        decimal? percentUsed = actual.Limit is { } used and > 0m
            ? decimal.Round(actual.Spent / used * 100m, PercentDecimals, MidpointRounding.AwayFromZero)
            : null;

        // Straight-line extrapolation of the rate so far. Crude, and honest about it:
        // it assumes the rest of the month looks like the part already spent.
        decimal? projected = isCurrentMonth && daysElapsed > 0
            ? decimal.Round(
                actual.Spent / daysElapsed * daysInMonth,
                MoneyDecimals,
                MidpointRounding.AwayFromZero)
            : null;

        return new BudgetStatusItem(
            actual.CategoryId,
            actual.CategoryName,
            actual.ColorHex,
            actual.Limit,
            actual.Spent,
            remaining,
            percentUsed,
            actual.Limit is { } cap && actual.Spent > cap,
            projected);
    }

    private static List<CategoryBreakdownItem> ToBreakdown(
        IReadOnlyList<CategoryTotal> totals,
        decimal grandTotal) =>
        totals
            .Select(total => new CategoryBreakdownItem(
                total.CategoryId,
                total.CategoryName,
                total.ColorHex,
                total.Total,
                total.Count,
                Percent(total.Total, grandTotal)))
            .ToList();

    private static List<PaymentMethodBreakdownItem> ToBreakdown(
        IReadOnlyList<PaymentMethodTotal> totals,
        decimal grandTotal) =>
        totals
            .Select(total => new PaymentMethodBreakdownItem(
                total.PaymentMethod,
                total.Total,
                total.Count,
                Percent(total.Total, grandTotal)))
            .ToList();

    /// <summary>Guards the empty-range case, where every share would otherwise divide by zero.</summary>
    private static decimal Percent(decimal part, decimal whole) =>
        whole == 0m
            ? 0m
            : decimal.Round(part / whole * 100m, PercentDecimals, MidpointRounding.AwayFromZero);

    private static List<TrendPoint> BuildDailyPoints(DateRange range, IReadOnlyList<PeriodTotal> totals)
    {
        var byDay = totals.ToDictionary(total => total.PeriodStart);
        var points = new List<TrendPoint>(range.DayCount);

        for (var day = range.From; day <= range.To; day = day.AddDays(1))
        {
            // Days with no spending are emitted as zero rather than skipped: a chart
            // needs a point per period to draw a continuous axis, and a gap would
            // otherwise read as a shorter month instead of a quiet one.
            var total = byDay.GetValueOrDefault(day);

            points.Add(new TrendPoint(day, day.ToString("yyyy-MM-dd"), total?.Total ?? 0m, total?.Count ?? 0));
        }

        return points;
    }

    private static List<TrendPoint> BuildMonthlyPoints(DateRange range, IReadOnlyList<PeriodTotal> totals)
    {
        var byMonth = totals.ToDictionary(total => total.PeriodStart);
        var points = new List<TrendPoint>();

        var current = BudgetMonth.From(range.From);
        var last = BudgetMonth.From(range.To);

        while (current <= last)
        {
            var total = byMonth.GetValueOrDefault(current.FirstDay);

            points.Add(new TrendPoint(
                current.FirstDay,
                current.ToString(),
                total?.Total ?? 0m,
                total?.Count ?? 0));

            current = current.AddMonths(1);
        }

        return points;
    }

    /// <summary>
    /// Fills in whichever end the caller left out, then hands the pair to
    /// <see cref="DateRange"/> so the ordering and span rules are enforced in one place.
    /// </summary>
    private DateRange ResolveRange(DateOnly? from, DateOnly? to)
    {
        var today = _clock.Today;

        return (from, to) switch
        {
            ({ } start, { } end) => DateRange.Create(start, end),
            ({ } start, null) => DateRange.Create(start, start > today ? start : today),
            (null, { } end) => DateRange.EndingOn(end, _options.DefaultReportDays),
            _ => DateRange.EndingOn(today, _options.DefaultReportDays)
        };
    }

    private BudgetMonth ResolveMonth(string? month)
    {
        if (string.IsNullOrWhiteSpace(month))
        {
            return BudgetMonth.From(_clock.Today);
        }

        return BudgetMonth.TryParse(month, out var parsed)
            ? parsed
            : throw new ValidationException(
                "month",
                $"'{month}' is not a valid month. Use 'yyyy-MM', e.g. '2026-10'.");
    }
}
