using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Domain.Entities;

namespace ExpenseTracker.Application.DTOs;

/// <summary>Payload for <c>POST /api/categories</c>.</summary>
public sealed record CreateCategoryRequest
{
    [Required]
    [StringLength(Category.MaxNameLength, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    /// <summary>A <c>#RRGGBB</c> swatch used by charts and the category chip.</summary>
    [Required]
    [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Colour must be a hex value such as '#2E7D32'.")]
    public string ColorHex { get; init; } = "#607D8B";
}

/// <summary>Payload for <c>PUT /api/categories/{id}</c>; null fields are left unchanged.</summary>
public sealed record UpdateCategoryRequest
{
    [StringLength(Category.MaxNameLength, MinimumLength = 1)]
    public string? Name { get; init; }

    [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Colour must be a hex value such as '#2E7D32'.")]
    public string? ColorHex { get; init; }

    /// <summary>`true` archives the category, `false` restores it.</summary>
    public bool? IsArchived { get; init; }
}

/// <param name="ExpenseCount">How many expenses are filed under this category, across all time.</param>
public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string ColorHex,
    bool IsArchived,
    int ExpenseCount,
    DateTimeOffset CreatedAt);
