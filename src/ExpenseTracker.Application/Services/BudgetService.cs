using Microsoft.Extensions.Options;
using ExpenseTracker.Application.DTOs;
using ExpenseTracker.Application.Exceptions;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Mapping;
using ExpenseTracker.Application.Options;
using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.ValueObjects;

namespace ExpenseTracker.Application.Services;

public sealed class BudgetService : IBudgetService
{
    private readonly IBudgetRepository _budgets;
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly string _currency;

    public BudgetService(
        IBudgetRepository budgets,
        ICategoryRepository categories,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IOptions<TrackerOptions> options)
    {
        _budgets = budgets;
        _categories = categories;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currency = options.Value.Currency;
    }

    public async Task<IReadOnlyList<BudgetResponse>> ListForMonthAsync(
        string month,
        CancellationToken cancellationToken = default)
    {
        var budgetMonth = ParseMonth(month);

        var rows = await _budgets.ListForMonthAsync(budgetMonth, cancellationToken);

        return rows.Select(row => row.ToResponse(_currency)).ToList();
    }

    public async Task<BudgetResponse> UpsertAsync(
        UpsertBudgetRequest request,
        CancellationToken cancellationToken = default)
    {
        var month = ParseMonth(request.Month);
        var limit = Money.From(request.Limit);
        var now = _clock.UtcNow;

        var category = await _categories.GetByIdAsync(request.CategoryId, cancellationToken)
                       ?? throw new ValidationException(
                           nameof(UpsertBudgetRequest.CategoryId),
                           $"No category exists with id {request.CategoryId}.");

        var existing = await _budgets.GetForAsync(category.Id, month, cancellationToken);

        if (existing is not null)
        {
            existing.ChangeLimit(limit, now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return existing.ToResponse(category, _currency);
        }

        var budget = Budget.Create(category, month, limit, now);

        await _budgets.AddAsync(budget, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (_unitOfWork.IsUniqueViolation(ex))
        {
            // Another caller inserted the same category-and-month between the read
            // above and this save. An upsert should still end with the requested
            // limit in place, so fold into the winning row rather than failing.
            _budgets.Remove(budget);

            var winner = await _budgets.GetForAsync(category.Id, month, cancellationToken)
                         ?? throw new ConflictException(
                             $"Could not save the budget for {month}. Try again.");

            winner.ChangeLimit(limit, now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return winner.ToResponse(category, _currency);
        }

        return budget.ToResponse(category, _currency);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var budget = await _budgets.GetByIdAsync(id, cancellationToken)
                     ?? throw NotFoundException.Budget(id);

        _budgets.Remove(budget);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static BudgetMonth ParseMonth(string? month) =>
        BudgetMonth.TryParse(month, out var parsed)
            ? parsed
            : throw new ValidationException(
                nameof(UpsertBudgetRequest.Month),
                $"'{month}' is not a valid month. Use 'yyyy-MM', e.g. '2026-10'.");
}
