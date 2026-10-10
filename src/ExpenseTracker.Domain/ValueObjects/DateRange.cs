using ExpenseTracker.Domain.Exceptions;

namespace ExpenseTracker.Domain.ValueObjects;

/// <summary>
/// An inclusive span of calendar days.
/// <para>
/// <see cref="DateOnly"/> throughout, never <see cref="DateTime"/>: an expense happens
/// on a day, not at an instant, so there is no timezone to get wrong and no
/// "is 23:59 on the 31st inside October?" question to answer.
/// </para>
/// <para>
/// Both ends are inclusive. A range of 1 October to 31 October covers all 31 days —
/// the half-open convention is more common in code, but every user asking for
/// "October" means the 31st included.
/// </para>
/// </summary>
public readonly record struct DateRange
{
    /// <summary>Roughly five years. Bounds how much a single report can scan.</summary>
    public const int MaxSpanDays = 1830;

    private DateRange(DateOnly from, DateOnly to)
    {
        From = from;
        To = to;
    }

    public DateOnly From { get; }

    public DateOnly To { get; }

    public int DayCount => To.DayNumber - From.DayNumber + 1;

    public static DateRange Create(DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            throw new InvalidDateRangeException("The end of a range cannot fall before its start.");
        }

        var dayCount = to.DayNumber - from.DayNumber + 1;

        if (dayCount > MaxSpanDays)
        {
            throw new InvalidDateRangeException(
                $"A range cannot span more than {MaxSpanDays} days; this one spans {dayCount}.");
        }

        return new DateRange(from, to);
    }

    /// <summary>The whole of one calendar month, with the correct last day for leap years.</summary>
    public static DateRange ForMonth(int year, int month) =>
        new(new DateOnly(year, month, 1), new DateOnly(year, month, DateTime.DaysInMonth(year, month)));

    /// <summary>The <paramref name="days"/> up to and including <paramref name="endingOn"/>.</summary>
    public static DateRange EndingOn(DateOnly endingOn, int days) =>
        Create(endingOn.AddDays(-(days - 1)), endingOn);

    public bool Contains(DateOnly date) => date >= From && date <= To;

    public override string ToString() => $"{From:yyyy-MM-dd}..{To:yyyy-MM-dd}";
}
