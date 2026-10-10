using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.Enums;

namespace ExpenseTracker.Application.DTOs;

/// <summary>Payload for <c>POST /api/expenses</c>.</summary>
public sealed record CreateExpenseRequest
{
    /// <summary>Must be greater than zero and have at most two decimal places.</summary>
    [Range(0.01, 1_000_000_000d)]
    public decimal Amount { get; init; }

    /// <summary>The day the money left, not the day this row was typed in.</summary>
    [Required]
    public DateOnly SpentOn { get; init; }

    [Required]
    public Guid CategoryId { get; init; }

    [Required]
    [StringLength(Expense.MaxDescriptionLength, MinimumLength = 1)]
    public string Description { get; init; } = string.Empty;

    public PaymentMethod PaymentMethod { get; init; } = PaymentMethod.Cash;

    [StringLength(Expense.MaxNotesLength)]
    public string? Notes { get; init; }
}

/// <summary>
/// Payload for <c>PUT /api/expenses/{id}</c>. Every field is optional: null means
/// "leave this alone", so a caller changing only the amount cannot wipe the notes.
/// </summary>
public sealed record UpdateExpenseRequest
{
    [Range(0.01, 1_000_000_000d)]
    public decimal? Amount { get; init; }

    public DateOnly? SpentOn { get; init; }

    public Guid? CategoryId { get; init; }

    [StringLength(Expense.MaxDescriptionLength, MinimumLength = 1)]
    public string? Description { get; init; }

    public PaymentMethod? PaymentMethod { get; init; }

    /// <summary>Send an empty string to clear the notes, since null means "unchanged".</summary>
    [StringLength(Expense.MaxNotesLength)]
    public string? Notes { get; init; }
}

/// <summary>An expense as the API returns it, with its category joined in for display.</summary>
public sealed record ExpenseResponse(
    Guid Id,
    decimal Amount,
    string Currency,
    DateOnly SpentOn,
    Guid CategoryId,
    string CategoryName,
    string CategoryColorHex,
    string Description,
    PaymentMethod PaymentMethod,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
