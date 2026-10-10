using Microsoft.EntityFrameworkCore;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Domain.Entities;

namespace ExpenseTracker.Infrastructure.Persistence.Repositories;

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly AppDbContext _db;

    public CategoryRepository(AppDbContext db) => _db = db;

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Categories.FirstOrDefaultAsync(category => category.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Category>> ListAsync(
        bool includeArchived,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Categories.AsNoTracking();

        if (!includeArchived)
        {
            query = query.Where(category => !category.IsArchived);
        }

        return await query
            .OrderBy(category => category.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> NameExistsAsync(
        string name,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        var normalised = name.Trim().ToLower();

        // ToLower on both sides rather than relying on SQL Server's case-insensitive
        // default collation: it costs an index seek on a table with a few dozen rows,
        // and buys the same answer on every provider, including the in-memory one
        // used by the tests, whose string comparison is ordinal.
        return _db.Categories
            .AsNoTracking()
            .AnyAsync(
                category => category.Name.ToLower() == normalised
                            && (excludingId == null || category.Id != excludingId),
                cancellationToken);
    }

    public Task<int> CountExpensesAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        _db.Expenses.CountAsync(expense => expense.CategoryId == categoryId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> CountExpensesByCategoryAsync(
        CancellationToken cancellationToken = default)
    {
        // One grouped query for the whole list instead of a count per category,
        // which is the difference between 2 round trips and N + 1.
        var counts = await _db.Expenses
            .AsNoTracking()
            .GroupBy(expense => expense.CategoryId)
            .Select(group => new { CategoryId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(row => row.CategoryId, row => row.Count);
    }

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default) =>
        await _db.Categories.AddAsync(category, cancellationToken);

    /// <summary>
    /// Removes a persisted entity, or detaches one whose insert was rejected — EF
    /// turns Remove on an Added entity into Detached, which is what the conflict
    /// paths need.
    /// </summary>
    public void Remove(Category category) => _db.Categories.Remove(category);
}
