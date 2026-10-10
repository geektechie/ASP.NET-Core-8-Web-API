using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ExpenseTracker.Domain.ValueObjects;

namespace ExpenseTracker.Infrastructure.Persistence.Converters;

/// <summary>
/// Maps <see cref="BudgetMonth"/> onto the first day of that month.
/// <para>
/// One sortable, indexable <c>date</c> column instead of a year/month pair, so
/// "budgets from March onwards" is a single range predicate rather than a composite
/// comparison.
/// </para>
/// <para>
/// Money has no converter here on purpose. A value-converted property cannot be
/// summed, averaged or maxed by the database, and reporting aggregates are most of
/// what this application does — so amounts stay plain decimals in the model and
/// <see cref="Money"/> guards them on the way in instead.
/// </para>
/// </summary>
public sealed class BudgetMonthConverter : ValueConverter<BudgetMonth, DateOnly>
{
    public BudgetMonthConverter()
        : base(
            month => month.FirstDay,
            date => BudgetMonth.From(date))
    {
    }
}
