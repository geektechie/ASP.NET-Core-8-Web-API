using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Domain.Enums;

namespace ExpenseTracker.Application.DTOs;

/// <summary>How a filtered list of expenses should be ordered.</summary>
public enum ExpenseSort
{
    /// <summary>Most recent spending first. The default, and what a ledger view wants.</summary>
    SpentOnDesc = 0,
    SpentOnAsc = 1,

    /// <summary>Biggest first — the ordering for "where did it all go?".</summary>
    AmountDesc = 2,
    AmountAsc = 3,

    /// <summary>Most recently entered first, regardless of when it was spent.</summary>
    CreatedAtDesc = 4
}

/// <summary>
/// Query-string filters for <c>GET /api/expenses</c>. Every filter is optional and
/// they combine with AND.
/// </summary>
public sealed class ExpenseQuery
{
    public const int MaxPageSize = 100;

    private int _page = 1;
    private int _pageSize = 25;

    [Range(1, int.MaxValue)]
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>Clamped rather than rejected, so an over-eager client degrades instead of failing.</summary>
    [Range(1, MaxPageSize)]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => 1,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    /// <summary>Inclusive lower bound on <c>spentOn</c>.</summary>
    public DateOnly? From { get; set; }

    /// <summary>Inclusive upper bound on <c>spentOn</c>.</summary>
    public DateOnly? To { get; set; }

    public Guid? CategoryId { get; set; }

    public PaymentMethod? PaymentMethod { get; set; }

    [Range(0, 1_000_000_000d)]
    public decimal? MinAmount { get; set; }

    [Range(0, 1_000_000_000d)]
    public decimal? MaxAmount { get; set; }

    /// <summary>Free text over the description and notes.</summary>
    [StringLength(200)]
    public string? Search { get; set; }

    public ExpenseSort Sort { get; set; } = ExpenseSort.SpentOnDesc;

    public int Skip => (Page - 1) * PageSize;
}
