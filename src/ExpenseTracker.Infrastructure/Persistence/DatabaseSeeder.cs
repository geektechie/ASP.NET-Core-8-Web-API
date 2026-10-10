using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Options;
using ExpenseTracker.Domain.Entities;

namespace ExpenseTracker.Infrastructure.Persistence;

/// <summary>
/// Puts a starter set of categories into an empty database.
/// <para>
/// Done at runtime rather than with <c>HasData</c> so it can be switched off by
/// configuration, and so it never fights with categories a user has renamed or
/// archived — a <c>HasData</c> seed is reapplied on every migration and would undo
/// exactly that.
/// </para>
/// </summary>
public sealed class DatabaseSeeder
{
    /// <summary>A deliberately small set: enough to file the first expense, not a taxonomy to prune.</summary>
    private static readonly (string Name, string ColorHex)[] DefaultCategories =
    [
        ("Groceries", "#2E7D32"),
        ("Dining out", "#EF6C00"),
        ("Transport", "#1565C0"),
        ("Housing", "#6A1B9A"),
        ("Utilities", "#00838F"),
        ("Health", "#C62828"),
        ("Entertainment", "#AD1457"),
        ("Shopping", "#4527A0"),
        ("Other", "#546E7A")
    ];

    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly TrackerOptions _options;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        AppDbContext db,
        IDateTimeProvider clock,
        IOptions<TrackerOptions> options,
        ILogger<DatabaseSeeder> logger)
    {
        _db = db;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.SeedDefaultCategories)
        {
            return;
        }

        // Only ever seeds a database with no categories at all. Topping up a
        // populated one would resurrect categories somebody deliberately deleted.
        if (await _db.Categories.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = _clock.UtcNow;

        var categories = DefaultCategories
            .Select(entry => Category.Create(entry.Name, entry.ColorHex, now))
            .ToList();

        _db.Categories.AddRange(categories);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeded {Count} default categories.", categories.Count);
    }
}
