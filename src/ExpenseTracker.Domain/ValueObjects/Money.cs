using System.Globalization;
using ExpenseTracker.Domain.Exceptions;

namespace ExpenseTracker.Domain.ValueObjects;

/// <summary>
/// A non-negative amount of money, rounded to the cent.
/// <para>
/// A value object rather than a bare <see cref="decimal"/> so the "no negatives, no
/// fractions of a cent" rule is enforced once, at the point of construction, instead
/// of being re-checked at every call site that happens to remember.
/// </para>
/// <para>
/// Single-currency by design: the currency is an application-wide setting used for
/// display, not a property of each amount. Multi-currency needs an exchange-rate
/// model, which is a different project.
/// </para>
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    public const int DecimalPlaces = 2;

    /// <summary>A ceiling that catches a misplaced decimal point or a units/cents mix-up.</summary>
    public const decimal MaxAmount = 1_000_000_000m;

    public static readonly Money Zero = new(0m);

    private Money(decimal amount) => Amount = amount;

    public decimal Amount { get; }

    public bool IsPositive => Amount > 0m;

    public static Money From(decimal amount)
    {
        if (amount < 0m)
        {
            throw new InvalidAmountException("Amount cannot be negative.");
        }

        if (amount > MaxAmount)
        {
            throw new InvalidAmountException($"Amount cannot exceed {MaxAmount:N0}.");
        }

        // Rejected rather than rounded. Silently turning 12.345 into 12.35 hides a
        // caller's bug, and whoever computed that third decimal is the one who should
        // decide which way it goes.
        if (decimal.Round(amount, DecimalPlaces) != amount)
        {
            throw new InvalidAmountException($"Amount cannot have more than {DecimalPlaces} decimal places.");
        }

        return new Money(amount);
    }

    /// <summary>
    /// Wraps a value that is already known to be valid, such as a sum computed by the
    /// database. Skips the decimal-places check because a SQL <c>SUM</c> over
    /// <c>decimal(18,2)</c> columns cannot produce a third decimal.
    /// </summary>
    public static Money FromTrustedTotal(decimal amount) =>
        new(decimal.Round(amount, DecimalPlaces, MidpointRounding.AwayFromZero));

    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount);

    /// <summary>Clamped at zero: spending less than a budget leaves nothing owed, not a negative amount.</summary>
    public static Money operator -(Money left, Money right) =>
        new(Math.Max(0m, left.Amount - right.Amount));

    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

    public static bool operator <(Money left, Money right) => left.Amount < right.Amount;

    public static bool operator >(Money left, Money right) => left.Amount > right.Amount;

    public static bool operator <=(Money left, Money right) => left.Amount <= right.Amount;

    public static bool operator >=(Money left, Money right) => left.Amount >= right.Amount;

    public override string ToString() => Amount.ToString("0.00", CultureInfo.InvariantCulture);
}
