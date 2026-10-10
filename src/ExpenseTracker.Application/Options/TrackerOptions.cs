using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Application.Options;

/// <summary>
/// Bound from the <c>Tracker</c> configuration section and validated at startup, so a
/// bad value fails the boot rather than the first request.
/// </summary>
public sealed class TrackerOptions
{
    public const string SectionName = "Tracker";

    /// <summary>
    /// ISO 4217 code echoed on every money-bearing response. Display only — amounts
    /// are stored as plain decimals and never converted.
    /// </summary>
    [Required]
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "Currency must be a three-letter ISO 4217 code, e.g. 'USD'.")]
    public string Currency { get; set; } = "USD";

    /// <summary>Window used by a report endpoint when the caller gives no dates.</summary>
    [Range(1, 366)]
    public int DefaultReportDays { get; set; } = 30;

    /// <summary>Seeds a starter set of categories into an empty database on first run.</summary>
    public bool SeedDefaultCategories { get; set; } = true;
}
