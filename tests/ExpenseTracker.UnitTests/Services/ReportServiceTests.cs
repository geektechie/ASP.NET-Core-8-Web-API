using ExpenseTracker.Application.DTOs;
using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Domain.Exceptions;
using ExpenseTracker.UnitTests.Support;
using ApplicationValidationException = ExpenseTracker.Application.Exceptions.ValidationException;

namespace ExpenseTracker.UnitTests.Services;

public sealed class ReportServiceTests
{
    private static readonly DateOnly OctoberFirst = new(2026, 10, 1);
    private static readonly DateOnly OctoberLast = new(2026, 10, 31);

    [Fact]
    public async Task Summary_totals_the_range_and_shares_it_by_category()
    {
        using var fixture = new TrackerFixture();

        var groceries = await fixture.AddCategoryAsync("Groceries");
        var transport = await fixture.AddCategoryAsync("Transport", "#1565C0");

        await fixture.AddExpenseAsync(60m, new DateOnly(2026, 10, 2), groceries.Id);
        await fixture.AddExpenseAsync(15m, new DateOnly(2026, 10, 5), groceries.Id);
        await fixture.AddExpenseAsync(25m, new DateOnly(2026, 10, 6), transport.Id);

        var summary = await fixture.Reports.GetSummaryAsync(OctoberFirst, OctoberLast);

        Assert.Equal(100m, summary.Total);
        Assert.Equal(3, summary.ExpenseCount);
        Assert.Equal(60m, summary.LargestExpense);
        Assert.Equal(31, summary.DayCount);

        // Averaged over every day in the range, not only the days with spending.
        Assert.Equal(3.23m, summary.DailyAverage);

        var byCategory = summary.ByCategory;
        Assert.Equal(2, byCategory.Count);

        var groceryRow = byCategory.Single(row => row.CategoryId == groceries.Id);
        Assert.Equal(75m, groceryRow.Total);
        Assert.Equal(2, groceryRow.Count);
        Assert.Equal(75m, groceryRow.PercentOfTotal);

        // Descending by total, so the biggest line is first.
        Assert.Equal(groceries.Id, byCategory[0].CategoryId);
    }

    [Fact]
    public async Task Summary_also_splits_by_payment_method()
    {
        using var fixture = new TrackerFixture();

        var groceries = await fixture.AddCategoryAsync("Groceries");

        await fixture.AddExpenseAsync(30m, new DateOnly(2026, 10, 2), groceries.Id, "A", PaymentMethod.Cash);
        await fixture.AddExpenseAsync(70m, new DateOnly(2026, 10, 3), groceries.Id, "B", PaymentMethod.CreditCard);

        var summary = await fixture.Reports.GetSummaryAsync(OctoberFirst, OctoberLast);

        var card = summary.ByPaymentMethod.Single(row => row.PaymentMethod == PaymentMethod.CreditCard);

        Assert.Equal(70m, card.Total);
        Assert.Equal(70m, card.PercentOfTotal);
    }

    [Fact]
    public async Task Summary_excludes_spending_outside_the_range()
    {
        using var fixture = new TrackerFixture();

        // Past the end of October, so an expense dated the 31st is history rather
        // than a future-dated entry the domain would reject.
        fixture.Clock.MoveTo(new DateOnly(2026, 11, 5));

        var groceries = await fixture.AddCategoryAsync("Groceries");

        await fixture.AddExpenseAsync(999m, new DateOnly(2026, 9, 30), groceries.Id);
        await fixture.AddExpenseAsync(10m, OctoberFirst, groceries.Id);
        await fixture.AddExpenseAsync(10m, OctoberLast, groceries.Id);

        var summary = await fixture.Reports.GetSummaryAsync(OctoberFirst, OctoberLast);

        // Both ends inclusive: the 1st and the 31st are in, the 30th of September is not.
        Assert.Equal(20m, summary.Total);
    }

    [Fact]
    public async Task Summary_of_an_empty_range_returns_zeroes_rather_than_dividing_by_zero()
    {
        using var fixture = new TrackerFixture();

        var summary = await fixture.Reports.GetSummaryAsync(OctoberFirst, OctoberLast);

        Assert.Equal(0m, summary.Total);
        Assert.Equal(0, summary.ExpenseCount);
        Assert.Equal(0m, summary.DailyAverage);
        Assert.Empty(summary.ByCategory);
    }

    [Fact]
    public async Task Summary_defaults_to_the_configured_window_ending_today()
    {
        using var fixture = new TrackerFixture(options => options.DefaultReportDays = 7);

        // The fixture clock sits at 2026-10-15.
        var summary = await fixture.Reports.GetSummaryAsync(null, null);

        Assert.Equal(new DateOnly(2026, 10, 9), summary.From);
        Assert.Equal(new DateOnly(2026, 10, 15), summary.To);
        Assert.Equal(7, summary.DayCount);
    }

    [Fact]
    public async Task Summary_rejects_an_inverted_range() =>
        await Assert.ThrowsAsync<InvalidDateRangeException>(async () =>
        {
            using var fixture = new TrackerFixture();
            await fixture.Reports.GetSummaryAsync(OctoberLast, OctoberFirst);
        });

    [Fact]
    public async Task Daily_trend_emits_a_point_for_every_day_including_empty_ones()
    {
        using var fixture = new TrackerFixture();

        var groceries = await fixture.AddCategoryAsync("Groceries");

        await fixture.AddExpenseAsync(10m, new DateOnly(2026, 10, 1), groceries.Id);
        await fixture.AddExpenseAsync(5m, new DateOnly(2026, 10, 1), groceries.Id);
        await fixture.AddExpenseAsync(20m, new DateOnly(2026, 10, 5), groceries.Id);

        var trend = await fixture.Reports.GetTrendAsync(
            OctoberFirst,
            new DateOnly(2026, 10, 5),
            TrendGranularity.Daily);

        // A chart needs a point per period to draw a continuous axis; a gap would
        // read as a shorter month rather than a quiet one.
        Assert.Equal(5, trend.Points.Count);
        Assert.Equal([15m, 0m, 0m, 0m, 20m], trend.Points.Select(point => point.Total));
        Assert.Equal(2, trend.Points[0].Count);
        Assert.Equal("2026-10-01", trend.Points[0].Label);
        Assert.Equal(35m, trend.Total);
    }

    [Fact]
    public async Task Monthly_trend_buckets_by_month_and_fills_quiet_months()
    {
        using var fixture = new TrackerFixture();

        fixture.Clock.MoveTo(new DateOnly(2026, 11, 5));

        var groceries = await fixture.AddCategoryAsync("Groceries");

        await fixture.AddExpenseAsync(100m, new DateOnly(2026, 8, 15), groceries.Id);
        await fixture.AddExpenseAsync(50m, new DateOnly(2026, 10, 3), groceries.Id);
        await fixture.AddExpenseAsync(25m, new DateOnly(2026, 10, 20), groceries.Id);

        var trend = await fixture.Reports.GetTrendAsync(
            new DateOnly(2026, 8, 1),
            OctoberLast,
            TrendGranularity.Monthly);

        Assert.Equal(["2026-08", "2026-09", "2026-10"], trend.Points.Select(point => point.Label));
        Assert.Equal([100m, 0m, 75m], trend.Points.Select(point => point.Total));
        Assert.Equal(new DateOnly(2026, 9, 1), trend.Points[1].PeriodStart);
    }

    [Fact]
    public async Task Monthly_trend_covers_a_partial_month_at_each_end()
    {
        using var fixture = new TrackerFixture();

        var groceries = await fixture.AddCategoryAsync("Groceries");
        await fixture.AddExpenseAsync(40m, new DateOnly(2026, 9, 20), groceries.Id);

        var trend = await fixture.Reports.GetTrendAsync(
            new DateOnly(2026, 9, 15),
            new DateOnly(2026, 10, 10),
            TrendGranularity.Monthly);

        Assert.Equal(["2026-09", "2026-10"], trend.Points.Select(point => point.Label));
        Assert.Equal(40m, trend.Points[0].Total);
    }

    [Fact]
    public async Task Budget_status_compares_each_category_against_its_limit()
    {
        using var fixture = new TrackerFixture();

        var groceries = await fixture.AddCategoryAsync("Groceries");
        var transport = await fixture.AddCategoryAsync("Transport", "#1565C0");

        await fixture.SetBudgetAsync(groceries.Id, "2026-10", 400m);
        await fixture.SetBudgetAsync(transport.Id, "2026-10", 100m);

        await fixture.AddExpenseAsync(300m, new DateOnly(2026, 10, 5), groceries.Id);
        await fixture.AddExpenseAsync(120m, new DateOnly(2026, 10, 6), transport.Id);

        var status = await fixture.Reports.GetBudgetStatusAsync("2026-10");

        Assert.Equal(500m, status.TotalLimit);
        Assert.Equal(420m, status.TotalSpent);
        Assert.Equal(80m, status.TotalRemaining);

        var groceryRow = status.Categories.Single(row => row.CategoryId == groceries.Id);
        Assert.Equal(400m, groceryRow.Limit);
        Assert.Equal(300m, groceryRow.Spent);
        Assert.Equal(100m, groceryRow.Remaining);
        Assert.Equal(75m, groceryRow.PercentUsed);
        Assert.False(groceryRow.IsOverBudget);

        var transportRow = status.Categories.Single(row => row.CategoryId == transport.Id);
        Assert.True(transportRow.IsOverBudget);
        Assert.Equal(120m, transportRow.PercentUsed);

        // Remaining never goes negative — overspending leaves nothing left, not a debt.
        Assert.Equal(0m, transportRow.Remaining);
    }

    [Fact]
    public async Task Budget_status_includes_spending_in_categories_nobody_budgeted_for()
    {
        using var fixture = new TrackerFixture();

        var groceries = await fixture.AddCategoryAsync("Groceries");
        var impulse = await fixture.AddCategoryAsync("Shopping", "#4527A0");

        await fixture.SetBudgetAsync(groceries.Id, "2026-10", 400m);
        await fixture.AddExpenseAsync(90m, new DateOnly(2026, 10, 4), impulse.Id);

        var status = await fixture.Reports.GetBudgetStatusAsync("2026-10");

        var unbudgeted = status.Categories.Single(row => row.CategoryId == impulse.Id);

        // Unbudgeted spending must not be able to hide from the report.
        Assert.Null(unbudgeted.Limit);
        Assert.Null(unbudgeted.PercentUsed);
        Assert.Null(unbudgeted.Remaining);
        Assert.False(unbudgeted.IsOverBudget);
        Assert.Equal(90m, unbudgeted.Spent);
    }

    [Fact]
    public async Task Budget_status_includes_a_budget_with_no_spending_yet()
    {
        using var fixture = new TrackerFixture();

        var groceries = await fixture.AddCategoryAsync("Groceries");
        await fixture.SetBudgetAsync(groceries.Id, "2026-10", 400m);

        var status = await fixture.Reports.GetBudgetStatusAsync("2026-10");

        var row = Assert.Single(status.Categories);
        Assert.Equal(0m, row.Spent);
        Assert.Equal(400m, row.Remaining);
        Assert.Equal(0m, row.PercentUsed);
    }

    [Fact]
    public async Task Budget_status_projects_the_month_in_progress()
    {
        using var fixture = new TrackerFixture();

        var groceries = await fixture.AddCategoryAsync("Groceries");
        await fixture.SetBudgetAsync(groceries.Id, "2026-10", 400m);
        await fixture.AddExpenseAsync(150m, new DateOnly(2026, 10, 10), groceries.Id);

        // The fixture clock sits at 2026-10-15, so 15 of 31 days have elapsed.
        var status = await fixture.Reports.GetBudgetStatusAsync("2026-10");

        Assert.True(status.IsCurrentMonth);
        Assert.Equal(15, status.DaysElapsed);
        Assert.Equal(31, status.DaysInMonth);

        // 150 / 15 * 31 = 310 at the current rate.
        Assert.Equal(310m, Assert.Single(status.Categories).ProjectedSpend);
    }

    [Fact]
    public async Task Budget_status_does_not_project_a_finished_month()
    {
        using var fixture = new TrackerFixture();

        var groceries = await fixture.AddCategoryAsync("Groceries");
        await fixture.SetBudgetAsync(groceries.Id, "2026-09", 400m);
        await fixture.AddExpenseAsync(150m, new DateOnly(2026, 9, 10), groceries.Id);

        var status = await fixture.Reports.GetBudgetStatusAsync("2026-09");

        Assert.False(status.IsCurrentMonth);
        Assert.Equal(30, status.DaysElapsed);
        Assert.Null(Assert.Single(status.Categories).ProjectedSpend);
    }

    [Fact]
    public async Task Budget_status_counts_only_the_month_it_was_asked_about()
    {
        using var fixture = new TrackerFixture();

        var groceries = await fixture.AddCategoryAsync("Groceries");
        await fixture.SetBudgetAsync(groceries.Id, "2026-10", 400m);

        await fixture.AddExpenseAsync(500m, new DateOnly(2026, 9, 30), groceries.Id);
        await fixture.AddExpenseAsync(50m, new DateOnly(2026, 10, 1), groceries.Id);

        var status = await fixture.Reports.GetBudgetStatusAsync("2026-10");

        Assert.Equal(50m, Assert.Single(status.Categories).Spent);
    }

    [Fact]
    public async Task Budget_status_defaults_to_the_current_month()
    {
        using var fixture = new TrackerFixture();

        var status = await fixture.Reports.GetBudgetStatusAsync(null);

        Assert.Equal("2026-10", status.Month);
        Assert.True(status.IsCurrentMonth);
    }

    [Fact]
    public async Task Budget_status_rejects_a_malformed_month()
    {
        using var fixture = new TrackerFixture();

        await Assert.ThrowsAsync<ApplicationValidationException>(
            () => fixture.Reports.GetBudgetStatusAsync("2026-13"));
    }
}
