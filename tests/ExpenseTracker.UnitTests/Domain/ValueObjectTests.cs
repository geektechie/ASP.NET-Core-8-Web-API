using ExpenseTracker.Domain.Exceptions;
using ExpenseTracker.Domain.ValueObjects;

namespace ExpenseTracker.UnitTests.Domain;

public sealed class MoneyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(0.01)]
    [InlineData(12.34)]
    [InlineData(1_000_000_000)]
    public void From_accepts_a_non_negative_amount_with_at_most_two_decimals(decimal amount) =>
        Assert.Equal(amount, Money.From(amount).Amount);

    [Fact]
    public void From_rejects_a_negative_amount() =>
        Assert.Throws<InvalidAmountException>(() => Money.From(-0.01m));

    [Fact]
    public void From_rejects_an_amount_past_the_ceiling() =>
        Assert.Throws<InvalidAmountException>(() => Money.From(Money.MaxAmount + 0.01m));

    [Theory]
    [InlineData(12.345)]
    [InlineData(0.001)]
    public void From_rejects_fractions_of_a_cent_rather_than_rounding(decimal amount)
    {
        // Silently turning 12.345 into 12.35 would hide the caller's bug, and whoever
        // computed that third decimal is the one who should decide which way it goes.
        var exception = Assert.Throws<InvalidAmountException>(() => Money.From(amount));

        Assert.Contains("decimal places", exception.Message);
    }

    [Fact]
    public void FromTrustedTotal_rounds_instead_of_throwing()
    {
        // The path used for database sums and for reading an existing column: a bad
        // legacy value must stay loadable so it can be corrected.
        Assert.Equal(12.35m, Money.FromTrustedTotal(12.345m).Amount);
    }

    [Fact]
    public void IsPositive_distinguishes_zero_from_a_real_amount()
    {
        Assert.False(Money.Zero.IsPositive);
        Assert.True(Money.From(0.01m).IsPositive);
    }

    [Fact]
    public void Subtraction_clamps_at_zero()
    {
        // Spending less than a budget leaves nothing owed, not a negative amount.
        Assert.Equal(0m, (Money.From(10m) - Money.From(25m)).Amount);
        Assert.Equal(15m, (Money.From(25m) - Money.From(10m)).Amount);
    }

    [Fact]
    public void Amounts_compare_by_value()
    {
        Assert.True(Money.From(10m) < Money.From(10.01m));
        Assert.True(Money.From(10m) >= Money.From(10m));
        Assert.Equal(Money.From(10m), Money.From(10.00m));
    }
}

public sealed class DateRangeTests
{
    [Fact]
    public void Both_ends_are_inclusive()
    {
        var range = DateRange.Create(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        Assert.Equal(31, range.DayCount);
        Assert.True(range.Contains(new DateOnly(2026, 10, 1)));
        Assert.True(range.Contains(new DateOnly(2026, 10, 31)));
        Assert.False(range.Contains(new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public void A_single_day_range_counts_as_one_day() =>
        Assert.Equal(1, DateRange.Create(new DateOnly(2026, 10, 9), new DateOnly(2026, 10, 9)).DayCount);

    [Fact]
    public void Create_rejects_an_inverted_range() =>
        Assert.Throws<InvalidDateRangeException>(() =>
            DateRange.Create(new DateOnly(2026, 10, 31), new DateOnly(2026, 10, 1)));

    [Fact]
    public void Create_rejects_a_range_past_the_span_limit()
    {
        var from = new DateOnly(2020, 1, 1);

        Assert.Throws<InvalidDateRangeException>(() =>
            DateRange.Create(from, from.AddDays(DateRange.MaxSpanDays)));
    }

    [Fact]
    public void ForMonth_lands_on_the_right_last_day_in_a_leap_year()
    {
        Assert.Equal(new DateOnly(2024, 2, 29), DateRange.ForMonth(2024, 2).To);
        Assert.Equal(new DateOnly(2026, 2, 28), DateRange.ForMonth(2026, 2).To);
    }

    [Fact]
    public void EndingOn_counts_the_end_day_itself()
    {
        var range = DateRange.EndingOn(new DateOnly(2026, 10, 15), 7);

        Assert.Equal(new DateOnly(2026, 10, 9), range.From);
        Assert.Equal(new DateOnly(2026, 10, 15), range.To);
        Assert.Equal(7, range.DayCount);
    }
}

public sealed class BudgetMonthTests
{
    [Fact]
    public void Create_rejects_a_month_outside_one_to_twelve()
    {
        Assert.Throws<InvalidDateRangeException>(() => BudgetMonth.Create(2026, 0));
        Assert.Throws<InvalidDateRangeException>(() => BudgetMonth.Create(2026, 13));
    }

    [Theory]
    [InlineData("2026-10", 2026, 10)]
    [InlineData("2026-01", 2026, 1)]
    public void TryParse_accepts_the_yyyy_MM_form(string value, int year, int month)
    {
        Assert.True(BudgetMonth.TryParse(value, out var parsed));
        Assert.Equal(year, parsed.Year);
        Assert.Equal(month, parsed.Month);
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026")]
    [InlineData("October 2026")]
    [InlineData("2026-10-09")]
    [InlineData(null)]
    [InlineData("")]
    public void TryParse_rejects_anything_else(string? value) =>
        Assert.False(BudgetMonth.TryParse(value, out _));

    [Fact]
    public void ToString_round_trips_through_TryParse()
    {
        var month = BudgetMonth.Create(2026, 3);

        Assert.Equal("2026-03", month.ToString());
        Assert.True(BudgetMonth.TryParse(month.ToString(), out var parsed));
        Assert.Equal(month, parsed);
    }

    [Fact]
    public void AddMonths_rolls_over_the_year_boundary()
    {
        var december = BudgetMonth.Create(2026, 12);

        Assert.Equal(BudgetMonth.Create(2027, 1), december.AddMonths(1));
        Assert.Equal(BudgetMonth.Create(2026, 11), december.AddMonths(-1));
    }

    [Fact]
    public void Months_compare_chronologically()
    {
        Assert.True(BudgetMonth.Create(2026, 1) < BudgetMonth.Create(2026, 2));
        Assert.True(BudgetMonth.Create(2027, 1) > BudgetMonth.Create(2026, 12));
    }

    [Fact]
    public void ToDateRange_covers_the_whole_month()
    {
        var range = BudgetMonth.Create(2026, 2).ToDateRange();

        Assert.Equal(new DateOnly(2026, 2, 1), range.From);
        Assert.Equal(new DateOnly(2026, 2, 28), range.To);
    }
}
