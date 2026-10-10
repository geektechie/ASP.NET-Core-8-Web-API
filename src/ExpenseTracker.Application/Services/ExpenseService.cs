using Microsoft.Extensions.Options;
using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.DTOs;
using ExpenseTracker.Application.Exceptions;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Mapping;
using ExpenseTracker.Application.Options;
using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.ValueObjects;

namespace ExpenseTracker.Application.Services;

public sealed class ExpenseService : IExpenseService
{
    private readonly IExpenseRepository _expenses;
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly string _currency;

    public ExpenseService(
        IExpenseRepository expenses,
        ICategoryRepository categories,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IOptions<TrackerOptions> options)
    {
        _expenses = expenses;
        _categories = categories;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currency = options.Value.Currency;
    }

    public async Task<PagedResult<ExpenseResponse>> QueryAsync(
        ExpenseQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateQuery(query);

        var page = await _expenses.QueryAsync(query, cancellationToken);

        return page.Map(row => row.ToResponse(_currency));
    }

    public async Task<ExpenseResponse> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _expenses.GetWithCategoryAsync(id, cancellationToken)
                  ?? throw NotFoundException.Expense(id);

        return row.ToResponse(_currency);
    }

    public async Task<ExpenseResponse> CreateAsync(
        CreateExpenseRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await RequireCategoryAsync(request.CategoryId, cancellationToken);

        var expense = Expense.Create(
            Money.From(request.Amount),
            request.SpentOn,
            category,
            request.Description,
            request.PaymentMethod,
            _clock.UtcNow,
            request.Notes);

        await _expenses.AddAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return expense.ToResponse(category, _currency);
    }

    public async Task<ExpenseResponse> UpdateAsync(
        Guid id,
        UpdateExpenseRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        var expense = await _expenses.GetByIdAsync(id, cancellationToken)
                      ?? throw NotFoundException.Expense(id);

        if (request.Amount is { } amount)
        {
            expense.ChangeAmount(Money.From(amount), now);
        }

        if (request.SpentOn is { } spentOn)
        {
            expense.ChangeSpentOn(spentOn, now);
        }

        if (request.CategoryId is { } categoryId && categoryId != expense.CategoryId)
        {
            expense.ChangeCategory(await RequireCategoryAsync(categoryId, cancellationToken), now);
        }

        if (request.Description is not null)
        {
            expense.ChangeDescription(request.Description, now);
        }

        if (request.PaymentMethod is { } paymentMethod)
        {
            expense.ChangePaymentMethod(paymentMethod, now);
        }

        if (request.Notes is not null)
        {
            // An empty string is the documented way to clear notes, since null
            // already means "do not touch this field".
            expense.ChangeNotes(request.Notes, now);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Re-read the category rather than assume it is unchanged: the update may
        // have moved the expense, and the response carries the category's name.
        var category = await _categories.GetByIdAsync(expense.CategoryId, cancellationToken)
                       ?? throw NotFoundException.Category(expense.CategoryId);

        return expense.ToResponse(category, _currency);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var expense = await _expenses.GetByIdAsync(id, cancellationToken)
                      ?? throw NotFoundException.Expense(id);

        // Expenses are deleted outright, unlike categories: one row, no history
        // hanging off it, and a mistyped expense should simply go away.
        _expenses.Remove(expense);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Category> RequireCategoryAsync(Guid categoryId, CancellationToken cancellationToken) =>
        await _categories.GetByIdAsync(categoryId, cancellationToken)
        // A 400, not a 404: the expense being created is not what is missing, the
        // category the caller pointed at in the body is.
        ?? throw new ValidationException(
            nameof(CreateExpenseRequest.CategoryId),
            $"No category exists with id {categoryId}.");

    private static void ValidateQuery(ExpenseQuery query)
    {
        if (query.From is { } from && query.To is { } to && to < from)
        {
            throw new ValidationException(nameof(ExpenseQuery.To), "'to' cannot fall before 'from'.");
        }

        if (query.MinAmount is { } min && query.MaxAmount is { } max && max < min)
        {
            throw new ValidationException(
                nameof(ExpenseQuery.MaxAmount),
                "'maxAmount' cannot be less than 'minAmount'.");
        }
    }
}
