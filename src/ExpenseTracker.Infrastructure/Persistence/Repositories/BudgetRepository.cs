using Microsoft.EntityFrameworkCore;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.ValueObjects;

namespace ExpenseTracker.Infrastructure.Persistence.Repositories;

public sealed class BudgetRepository : IBudgetRepository
{
    private readonly AppDbContext _db;

    public BudgetRepository(AppDbContext db) => _db = db;

    public Task<Budget?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Budgets.FirstOrDefaultAsync(budget => budget.Id == id, cancellationToken);

    public Task<Budget?> GetForAsync(
        Guid categoryId,
        BudgetMonth month,
        CancellationToken cancellationToken = default) =>
        // Tracked, because the upsert path mutates whatever comes back.
        _db.Budgets.FirstOrDefaultAsync(
            budget => budget.CategoryId == categoryId && budget.Month == month,
            cancellationToken);

    public async Task<BudgetWithCategory?> GetWithCategoryAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var budget = await _db.Budgets
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (budget is null)
        {
            return null;
        }

        var attached = await AttachCategoriesAsync([budget], cancellationToken);

        return attached[0];
    }

    public async Task<IReadOnlyList<BudgetWithCategory>> ListForMonthAsync(
        BudgetMonth month,
        CancellationToken cancellationToken = default)
    {
        var budgets = await _db.Budgets
            .AsNoTracking()
            .Where(budget => budget.Month == month)
            .ToListAsync(cancellationToken);

        var attached = await AttachCategoriesAsync(budgets, cancellationToken);

        return attached
            .OrderBy(row => row.CategoryName)
            .ToList();
    }

    public async Task AddAsync(Budget budget, CancellationToken cancellationToken = default) =>
        await _db.Budgets.AddAsync(budget, cancellationToken);

    public void Remove(Budget budget) => _db.Budgets.Remove(budget);

    /// <summary>
    /// Pairs each budget with its category's display fields, in one lookup against a
    /// table that holds a few dozen rows at most.
    /// </summary>
    private async Task<List<BudgetWithCategory>> AttachCategoriesAsync(
        List<Budget> budgets,
        CancellationToken cancellationToken)
    {
        if (budgets.Count == 0)
        {
            return [];
        }

        var categoryIds = budgets
            .Select(budget => budget.CategoryId)
            .Distinct()
            .ToList();

        var categories = await _db.Categories
            .AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, cancellationToken);

        return budgets
            .Select(budget =>
            {
                var category = categories[budget.CategoryId];

                return new BudgetWithCategory(budget, category.Name, category.ColorHex);
            })
            .ToList();
    }
}
