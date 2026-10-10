using Microsoft.EntityFrameworkCore;
using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.DTOs;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Domain.Entities;

namespace ExpenseTracker.Infrastructure.Persistence.Repositories;

public sealed class ExpenseRepository : IExpenseRepository
{
    private readonly AppDbContext _db;

    public ExpenseRepository(AppDbContext db) => _db = db;

    public Task<Expense?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Expenses.FirstOrDefaultAsync(expense => expense.Id == id, cancellationToken);

    public async Task<ExpenseWithCategory?> GetWithCategoryAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var expense = await _db.Expenses
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (expense is null)
        {
            return null;
        }

        var attached = await AttachCategoriesAsync([expense], cancellationToken);

        return attached[0];
    }

    public async Task<PagedResult<ExpenseWithCategory>> QueryAsync(
        ExpenseQuery query,
        CancellationToken cancellationToken = default)
    {
        var filtered = ApplyFilters(_db.Expenses.AsNoTracking(), query);

        var totalCount = await filtered.LongCountAsync(cancellationToken);

        if (totalCount == 0)
        {
            return PagedResult<ExpenseWithCategory>.Empty(query.Page, query.PageSize);
        }

        // Sorting and paging happen in SQL over the expenses alone; the category
        // names are fetched separately below. Joining here instead would push the
        // ORDER BY into a subquery, where the engine is free to ignore it.
        var expenses = await ApplySort(filtered, query.Sort)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = await AttachCategoriesAsync(expenses, cancellationToken);

        return new PagedResult<ExpenseWithCategory>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task AddAsync(Expense expense, CancellationToken cancellationToken = default) =>
        await _db.Expenses.AddAsync(expense, cancellationToken);

    public void Remove(Expense expense) => _db.Expenses.Remove(expense);

    /// <summary>
    /// Pairs each expense with its category's display fields.
    /// <para>
    /// A second query rather than a join, because the page holds at most a hundred
    /// expenses drawn from a handful of categories — one small <c>IN</c> lookup
    /// against a tiny table, against a join that would otherwise repeat the category
    /// name on every row and complicate the ordering.
    /// </para>
    /// </summary>
    private async Task<List<ExpenseWithCategory>> AttachCategoriesAsync(
        List<Expense> expenses,
        CancellationToken cancellationToken)
    {
        if (expenses.Count == 0)
        {
            return [];
        }

        var categoryIds = expenses
            .Select(expense => expense.CategoryId)
            .Distinct()
            .ToList();

        var categories = await _db.Categories
            .AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, cancellationToken);

        return expenses
            .Select(expense =>
            {
                // A missing key here would mean a broken foreign key, which the
                // schema does not allow — so letting it throw is the right outcome.
                var category = categories[expense.CategoryId];

                return new ExpenseWithCategory(expense, category.Name, category.ColorHex);
            })
            .ToList();
    }

    private static IQueryable<Expense> ApplyFilters(IQueryable<Expense> expenses, ExpenseQuery query)
    {
        if (query.From is { } from)
        {
            expenses = expenses.Where(expense => expense.SpentOn >= from);
        }

        if (query.To is { } to)
        {
            expenses = expenses.Where(expense => expense.SpentOn <= to);
        }

        if (query.CategoryId is { } categoryId)
        {
            expenses = expenses.Where(expense => expense.CategoryId == categoryId);
        }

        if (query.PaymentMethod is { } paymentMethod)
        {
            expenses = expenses.Where(expense => expense.PaymentMethod == paymentMethod);
        }

        if (query.MinAmount is { } minAmount)
        {
            expenses = expenses.Where(expense => expense.Amount >= minAmount);
        }

        if (query.MaxAmount is { } maxAmount)
        {
            expenses = expenses.Where(expense => expense.Amount <= maxAmount);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();

            expenses = expenses.Where(expense =>
                expense.Description.Contains(term)
                || (expense.Notes != null && expense.Notes.Contains(term)));
        }

        return expenses;
    }

    /// <summary>
    /// Every ordering ends with the id. Without that tie-breaker, two expenses on the
    /// same day can swap places between page 1 and page 2, so a row is shown twice or
    /// never.
    /// </summary>
    private static IQueryable<Expense> ApplySort(IQueryable<Expense> expenses, ExpenseSort sort) =>
        sort switch
        {
            ExpenseSort.SpentOnAsc => expenses
                .OrderBy(expense => expense.SpentOn)
                .ThenBy(expense => expense.Id),

            ExpenseSort.AmountDesc => expenses
                .OrderByDescending(expense => expense.Amount)
                .ThenBy(expense => expense.Id),

            ExpenseSort.AmountAsc => expenses
                .OrderBy(expense => expense.Amount)
                .ThenBy(expense => expense.Id),

            ExpenseSort.CreatedAtDesc => expenses
                .OrderByDescending(expense => expense.CreatedAt)
                .ThenBy(expense => expense.Id),

            _ => expenses
                .OrderByDescending(expense => expense.SpentOn)
                .ThenByDescending(expense => expense.CreatedAt)
                .ThenBy(expense => expense.Id)
        };
}
