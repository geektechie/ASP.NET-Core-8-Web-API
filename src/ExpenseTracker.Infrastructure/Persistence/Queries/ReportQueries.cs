using Microsoft.EntityFrameworkCore;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.ValueObjects;

namespace ExpenseTracker.Infrastructure.Persistence.Queries;

/// <summary>
/// The read side: every total here is computed by the database.
/// <para>
/// None of these load expenses into memory to add them up. A year of spending can be
/// tens of thousands of rows, and the answer is always a handful of numbers — so the
/// grouping belongs in SQL, where an index can serve it.
/// </para>
/// </summary>
public sealed class ReportQueries : IReportQueries
{
    private readonly AppDbContext _db;

    public ReportQueries(AppDbContext db) => _db = db;

    public async Task<SpendingTotals> GetTotalsAsync(
        DateRange range,
        CancellationToken cancellationToken = default)
    {
        // Grouping by a constant collapses the whole filtered set into one row, so
        // the three aggregates come back in a single round trip.
        var totals = await InRange(range)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Sum(expense => expense.Amount),
                Count = group.Count(),
                Largest = group.Max(expense => expense.Amount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return totals is null
            ? SpendingTotals.Empty
            : new SpendingTotals(totals.Total, totals.Count, totals.Largest);
    }

    public async Task<IReadOnlyList<CategoryTotal>> GetCategoryTotalsAsync(
        DateRange range,
        CancellationToken cancellationToken = default)
    {
        var rows = await (
                from expense in InRange(range)
                join category in _db.Categories.AsNoTracking() on expense.CategoryId equals category.Id
                group expense by new { category.Id, category.Name, category.ColorHex }
                into grouped
                select new
                {
                    grouped.Key.Id,
                    grouped.Key.Name,
                    grouped.Key.ColorHex,
                    Total = grouped.Sum(expense => expense.Amount),
                    Count = grouped.Count()
                })
            .OrderByDescending(row => row.Total)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new CategoryTotal(row.Id, row.Name, row.ColorHex, row.Total, row.Count))
            .ToList();
    }

    public async Task<IReadOnlyList<PaymentMethodTotal>> GetPaymentMethodTotalsAsync(
        DateRange range,
        CancellationToken cancellationToken = default)
    {
        var rows = await InRange(range)
            .GroupBy(expense => expense.PaymentMethod)
            .Select(group => new
            {
                PaymentMethod = group.Key,
                Total = group.Sum(expense => expense.Amount),
                Count = group.Count()
            })
            .OrderByDescending(row => row.Total)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new PaymentMethodTotal(row.PaymentMethod, row.Total, row.Count))
            .ToList();
    }

    public async Task<IReadOnlyList<PeriodTotal>> GetPeriodTotalsAsync(
        DateRange range,
        bool monthly,
        CancellationToken cancellationToken = default)
    {
        if (!monthly)
        {
            var daily = await InRange(range)
                .GroupBy(expense => expense.SpentOn)
                .Select(group => new
                {
                    Day = group.Key,
                    Total = group.Sum(expense => expense.Amount),
                    Count = group.Count()
                })
                .ToListAsync(cancellationToken);

            return daily
                .Select(row => new PeriodTotal(row.Day, row.Total, row.Count))
                .OrderBy(row => row.PeriodStart)
                .ToList();
        }

        // Grouped on the date parts rather than a computed first-of-month, because
        // Year and Month translate to DATEPART while constructing a DateOnly in the
        // projection would not translate at all.
        var monthlyRows = await InRange(range)
            .GroupBy(expense => new { expense.SpentOn.Year, expense.SpentOn.Month })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                Total = group.Sum(expense => expense.Amount),
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        return monthlyRows
            .Select(row => new PeriodTotal(new DateOnly(row.Year, row.Month, 1), row.Total, row.Count))
            .OrderBy(row => row.PeriodStart)
            .ToList();
    }

    public async Task<IReadOnlyList<CategoryBudgetActual>> GetBudgetActualsAsync(
        BudgetMonth month,
        CancellationToken cancellationToken = default)
    {
        var range = month.ToDateRange();

        // Spend and budgets are fetched separately and combined here because the
        // report needs the union of both sides — a category can have a budget and no
        // spending, or spending and no budget — and LINQ has no full outer join.
        // Each query returns at most one row per category, so this stays small.
        var spendRows = await InRange(range)
            .GroupBy(expense => expense.CategoryId)
            .Select(group => new
            {
                CategoryId = group.Key,
                Spent = group.Sum(expense => expense.Amount)
            })
            .ToListAsync(cancellationToken);

        var budgetRows = await _db.Budgets
            .AsNoTracking()
            .Where(budget => budget.Month == month)
            .Select(budget => new { budget.CategoryId, budget.Limit })
            .ToListAsync(cancellationToken);

        var spentByCategory = spendRows.ToDictionary(row => row.CategoryId, row => row.Spent);
        var limitByCategory = budgetRows.ToDictionary(row => row.CategoryId, row => row.Limit);

        var categoryIds = spentByCategory.Keys.Union(limitByCategory.Keys).ToList();

        if (categoryIds.Count == 0)
        {
            return [];
        }

        var categories = await _db.Categories
            .AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .ToListAsync(cancellationToken);

        return categories
            .Select(category => new CategoryBudgetActual(
                category.Id,
                category.Name,
                category.ColorHex,
                limitByCategory.TryGetValue(category.Id, out var limit) ? limit : null,
                spentByCategory.GetValueOrDefault(category.Id)))
            .ToList();
    }

    /// <summary>
    /// The shared filter behind every report. Both ends inclusive, matching
    /// <see cref="DateRange"/>, and bound to locals so EF parameterises them rather
    /// than trying to translate a struct member access.
    /// </summary>
    private IQueryable<Expense> InRange(DateRange range)
    {
        var from = range.From;
        var to = range.To;

        return _db.Expenses
            .AsNoTracking()
            .Where(expense => expense.SpentOn >= from && expense.SpentOn <= to);
    }
}
