using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Application.DTOs;

/// <summary>
/// Payload for <c>PUT /api/budgets</c>. An upsert keyed on category and month: there
/// is only ever one budget per pair, so creating and updating are the same request.
/// </summary>
public sealed record UpsertBudgetRequest
{
    [Required]
    public Guid CategoryId { get; init; }

    /// <summary>The month in <c>yyyy-MM</c> form, e.g. <c>2026-10</c>.</summary>
    [Required]
    [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$", ErrorMessage = "Month must be in 'yyyy-MM' form, e.g. '2026-10'.")]
    public string Month { get; init; } = string.Empty;

    [Range(0.01, 1_000_000_000d)]
    public decimal Limit { get; init; }
}

public sealed record BudgetResponse(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string CategoryColorHex,
    string Month,
    decimal Limit,
    string Currency,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
