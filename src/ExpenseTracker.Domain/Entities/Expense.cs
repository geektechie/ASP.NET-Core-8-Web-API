using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Domain.Exceptions;
using ExpenseTracker.Domain.ValueObjects;

namespace ExpenseTracker.Domain.Entities;

/// <summary>
/// One thing you spent money on. The aggregate root of the tracker.
/// </summary>
public sealed class Expense
{
    public const int MaxDescriptionLength = 200;
    public const int MaxNotesLength = 1000;

    /// <summary>
    /// How far ahead an expense may be dated. Zero would reject a card payment
    /// entered from a receipt whose terminal clock sits in tomorrow's timezone.
    /// </summary>
    public const int MaxDaysInFuture = 1;

    /// <summary>Required by EF Core for materialisation; not for application use.</summary>
    private Expense()
    {
        Description = null!;
    }

    private Expense(
        Money amount,
        DateOnly spentOn,
        Guid categoryId,
        string description,
        PaymentMethod paymentMethod,
        string? notes,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        Amount = amount.Amount;
        SpentOn = spentOn;
        CategoryId = categoryId;
        Description = description;
        PaymentMethod = paymentMethod;
        Notes = notes;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// A plain decimal, deliberately.
    /// <para>
    /// Writes go through <see cref="Money"/> — every constructor and mutator here takes
    /// one, so "positive, at most two decimal places, under the ceiling" is enforced
    /// on the way in. The stored type stays <c>decimal</c> because a value-converted
    /// property cannot be summed, averaged or maxed by the database, and reporting
    /// aggregates are most of what this application does.
    /// </para>
    /// </summary>
    public decimal Amount { get; private set; }

    /// <summary>The day the money left, not the day the row was typed in.</summary>
    public DateOnly SpentOn { get; private set; }

    public Guid CategoryId { get; private set; }

    public string Description { get; private set; }

    public PaymentMethod PaymentMethod { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <param name="category">
    /// Passed as the entity, not just an id, so the archived check cannot be skipped
    /// by a caller who happens to have an id to hand.
    /// </param>
    public static Expense Create(
        Money amount,
        DateOnly spentOn,
        Category category,
        string description,
        PaymentMethod paymentMethod,
        DateTimeOffset now,
        string? notes = null)
    {
        ArgumentNullException.ThrowIfNull(category);

        category.EnsureAcceptsNewExpenses();

        return new Expense(
            EnsurePositive(amount),
            EnsureNotTooFarAhead(spentOn, now),
            category.Id,
            NormaliseDescription(description),
            EnsureKnown(paymentMethod),
            NormaliseNotes(notes),
            now);
    }

    public void ChangeAmount(Money amount, DateTimeOffset now)
    {
        Amount = EnsurePositive(amount).Amount;
        Touch(now);
    }

    public void ChangeSpentOn(DateOnly spentOn, DateTimeOffset now)
    {
        SpentOn = EnsureNotTooFarAhead(spentOn, now);
        Touch(now);
    }

    public void ChangeCategory(Category category, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(category);

        category.EnsureAcceptsNewExpenses();

        CategoryId = category.Id;
        Touch(now);
    }

    public void ChangeDescription(string description, DateTimeOffset now)
    {
        Description = NormaliseDescription(description);
        Touch(now);
    }

    public void ChangePaymentMethod(PaymentMethod paymentMethod, DateTimeOffset now)
    {
        PaymentMethod = EnsureKnown(paymentMethod);
        Touch(now);
    }

    public void ChangeNotes(string? notes, DateTimeOffset now)
    {
        Notes = NormaliseNotes(notes);
        Touch(now);
    }

    private void Touch(DateTimeOffset now) => UpdatedAt = now;

    private static Money EnsurePositive(Money amount) =>
        amount.IsPositive
            ? amount
            : throw new InvalidAmountException("An expense must be greater than zero.");

    private static DateOnly EnsureNotTooFarAhead(DateOnly spentOn, DateTimeOffset now)
    {
        var latest = DateOnly.FromDateTime(now.UtcDateTime).AddDays(MaxDaysInFuture);

        return spentOn <= latest
            ? spentOn
            : throw new InvalidDateRangeException(
                $"An expense cannot be dated more than {MaxDaysInFuture} day(s) in the future.");
    }

    /// <summary>
    /// A value cast from an unknown integer is still the enum's type, so without this
    /// a request carrying <c>paymentMethod: 42</c> would be stored verbatim.
    /// </summary>
    private static PaymentMethod EnsureKnown(PaymentMethod paymentMethod) =>
        Enum.IsDefined(paymentMethod)
            ? paymentMethod
            : throw new InvalidTextException($"'{(int)paymentMethod}' is not a known payment method.");

    private static string NormaliseDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new InvalidTextException("A description is required.");
        }

        var trimmed = description.Trim();

        return trimmed.Length > MaxDescriptionLength
            ? throw new InvalidTextException($"Description may not exceed {MaxDescriptionLength} characters.")
            : trimmed;
    }

    private static string? NormaliseNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        var trimmed = notes.Trim();

        return trimmed.Length > MaxNotesLength
            ? throw new InvalidTextException($"Notes may not exceed {MaxNotesLength} characters.")
            : trimmed;
    }
}
