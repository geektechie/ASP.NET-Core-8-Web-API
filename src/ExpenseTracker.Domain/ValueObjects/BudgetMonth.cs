using System.Globalization;
using ExpenseTracker.Domain.Exceptions;

namespace ExpenseTracker.Domain.ValueObjects;

/// <summary>
/// A single calendar month, the period a budget is set for.
/// <para>
/// Stored as the first day of the month so the database keeps one sortable,
/// range-queryable <c>date</c> column instead of a year/month pair that needs two
/// predicates and a composite index to query.
/// </para>
/// </summary>
public readonly record struct BudgetMonth : IComparable<BudgetMonth>
{
    public const int MinYear = 2000;
    public const int MaxYear = 2100;

    private BudgetMonth(int year, int month)
    {
        Year = year;
        Month = month;
    }

    public int Year { get; }

    public int Month { get; }

    public DateOnly FirstDay => new(Year, Month, 1);

    public static BudgetMonth Create(int year, int month)
    {
        if (year is < MinYear or > MaxYear)
        {
            throw new InvalidDateRangeException($"Year must be between {MinYear} and {MaxYear}.");
        }

        if (month is < 1 or > 12)
        {
            throw new InvalidDateRangeException("Month must be between 1 and 12.");
        }

        return new BudgetMonth(year, month);
    }

    public static BudgetMonth From(DateOnly date) => new(date.Year, date.Month);

    /// <summary>Parses the <c>yyyy-MM</c> form the API accepts, e.g. <c>2026-10</c>.</summary>
    public static bool TryParse(string? value, out BudgetMonth month)
    {
        month = default;

        if (!DateOnly.TryParseExact(
                $"{value}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return false;
        }

        if (parsed.Year is < MinYear or > MaxYear)
        {
            return false;
        }

        month = new BudgetMonth(parsed.Year, parsed.Month);
        return true;
    }

    public DateRange ToDateRange() => DateRange.ForMonth(Year, Month);

    public BudgetMonth AddMonths(int count) => From(FirstDay.AddMonths(count));

    public int CompareTo(BudgetMonth other) => FirstDay.CompareTo(other.FirstDay);

    public static bool operator <(BudgetMonth left, BudgetMonth right) => left.CompareTo(right) < 0;

    public static bool operator >(BudgetMonth left, BudgetMonth right) => left.CompareTo(right) > 0;

    public static bool operator <=(BudgetMonth left, BudgetMonth right) => left.CompareTo(right) <= 0;

    public static bool operator >=(BudgetMonth left, BudgetMonth right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"{Year:D4}-{Month:D2}";
}
