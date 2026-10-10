using ExpenseTracker.Application.Models;
using ExpenseTracker.Domain.ValueObjects;

namespace ExpenseTracker.Application.Interfaces;

/// <summary>
/// The read side of the tracker: aggregates computed by the database, not by loading
/// expenses and adding them up in C#.
/// <para>
/// Deliberately separate from <see cref="IExpenseRepository"/>. Reports never go
/// through the aggregate — there is nothing to enforce when you are only summing —
/// and keeping them apart means a reporting query can be rewritten or moved to a read
/// replica without touching the write model.
/// </para>
/// </summary>
public interface IReportQueries
{
    /// <summary>Total, count and largest single expense across the range.</summary>
    Task<SpendingTotals> GetTotalsAsync(DateRange range, CancellationToken cancellationToken = default);

    /// <summary>Spending grouped by category, descending by total.</summary>
    Task<IReadOnlyList<CategoryTotal>> GetCategoryTotalsAsync(
        DateRange range,
        CancellationToken cancellationToken = default);

    /// <summary>Spending grouped by payment method, descending by total.</summary>
    Task<IReadOnlyList<PaymentMethodTotal>> GetPaymentMethodTotalsAsync(
        DateRange range,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Spending bucketed by day or month. Returns only periods that have spending —
    /// filling the gaps is the service's job, because it is the one that knows the
    /// requested range.
    /// </summary>
    Task<IReadOnlyList<PeriodTotal>> GetPeriodTotalsAsync(
        DateRange range,
        bool monthly,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Budget and actual side by side for one month, covering every category that has
    /// either, so unbudgeted spending still shows up.
    /// </summary>
    Task<IReadOnlyList<CategoryBudgetActual>> GetBudgetActualsAsync(
        BudgetMonth month,
        CancellationToken cancellationToken = default);
}
