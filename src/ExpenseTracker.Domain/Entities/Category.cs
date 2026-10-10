using System.Text.RegularExpressions;
using ExpenseTracker.Domain.Exceptions;

namespace ExpenseTracker.Domain.Entities;

/// <summary>
/// A spending category. Expenses and budgets both hang off one.
/// </summary>
public sealed partial class Category
{
    public const int MaxNameLength = 60;

    /// <summary>Required by EF Core for materialisation; not for application use.</summary>
    private Category()
    {
        Name = null!;
        ColorHex = null!;
    }

    private Category(string name, string colorHex, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        Name = name;
        ColorHex = colorHex;
        CreatedAt = createdAt;
        IsArchived = false;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    /// <summary>A <c>#RRGGBB</c> swatch, so a chart legend is stable rather than reassigned each render.</summary>
    public string ColorHex { get; private set; }

    public bool IsArchived { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Category Create(string name, string colorHex, DateTimeOffset createdAt) =>
        new(NormaliseName(name), NormaliseColor(colorHex), createdAt);

    public void Rename(string name) => Name = NormaliseName(name);

    public void ChangeColor(string colorHex) => ColorHex = NormaliseColor(colorHex);

    /// <summary>
    /// Hides the category from pickers without touching history. Deleting it would
    /// either orphan every expense filed under it or silently rewrite months of
    /// reports, so a category that has ever been used is archived, never removed.
    /// </summary>
    public void Archive() => IsArchived = true;

    public void Restore() => IsArchived = false;

    /// <summary>Guards the rule that new spending cannot be filed under a retired category.</summary>
    public void EnsureAcceptsNewExpenses()
    {
        if (IsArchived)
        {
            throw new InvalidOperationForStateException(
                $"Category '{Name}' is archived and cannot take new expenses. Restore it first.");
        }
    }

    private static string NormaliseName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidTextException("Category name is required.");
        }

        var trimmed = name.Trim();

        return trimmed.Length > MaxNameLength
            ? throw new InvalidTextException($"Category name may not exceed {MaxNameLength} characters.")
            : trimmed;
    }

    private static string NormaliseColor(string colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
        {
            throw new InvalidTextException("Category colour is required.");
        }

        var trimmed = colorHex.Trim();

        if (!ColorPattern().IsMatch(trimmed))
        {
            throw new InvalidTextException("Category colour must be a hex colour such as '#2E7D32'.");
        }

        // Upper-cased so two categories cannot look different in the database while
        // rendering identically in a chart.
        return trimmed.ToUpperInvariant();
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex ColorPattern();
}
