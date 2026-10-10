using ExpenseTracker.Application.DTOs;
using ExpenseTracker.Application.Exceptions;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Mapping;
using ExpenseTracker.Domain.Entities;

namespace ExpenseTracker.Application.Services;

public sealed class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public CategoryService(
        ICategoryRepository categories,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<IReadOnlyList<CategoryResponse>> ListAsync(
        bool includeArchived,
        CancellationToken cancellationToken = default)
    {
        var categories = await _categories.ListAsync(includeArchived, cancellationToken);

        // One grouped count for the whole list rather than a count per category —
        // the difference between 2 queries and N+1.
        var counts = await _categories.CountExpensesByCategoryAsync(cancellationToken);

        return categories
            .Select(category => category.ToResponse(counts.GetValueOrDefault(category.Id)))
            .ToList();
    }

    public async Task<CategoryResponse> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _categories.GetByIdAsync(id, cancellationToken)
                       ?? throw NotFoundException.Category(id);

        var count = await _categories.CountExpensesAsync(id, cancellationToken);

        return category.ToResponse(count);
    }

    public async Task<CategoryResponse> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        if (await _categories.NameExistsAsync(name, cancellationToken: cancellationToken))
        {
            throw ConflictException.CategoryNameTaken(name);
        }

        var category = Category.Create(name, request.ColorHex, _clock.UtcNow);

        await _categories.AddAsync(category, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (_unitOfWork.IsUniqueViolation(ex))
        {
            // The check above is only there for a friendlier message; the unique
            // index is what actually decides a race between two concurrent creators.
            _categories.Remove(category);
            throw ConflictException.CategoryNameTaken(name);
        }

        return category.ToResponse(0);
    }

    public async Task<CategoryResponse> UpdateAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await _categories.GetByIdAsync(id, cancellationToken)
                       ?? throw NotFoundException.Category(id);

        if (request.Name is not null)
        {
            var name = request.Name.Trim();

            if (await _categories.NameExistsAsync(name, id, cancellationToken))
            {
                throw ConflictException.CategoryNameTaken(name);
            }

            category.Rename(name);
        }

        if (request.ColorHex is not null)
        {
            category.ChangeColor(request.ColorHex);
        }

        switch (request.IsArchived)
        {
            case true:
                category.Archive();
                break;
            case false:
                category.Restore();
                break;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var count = await _categories.CountExpensesAsync(id, cancellationToken);

        return category.ToResponse(count);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _categories.GetByIdAsync(id, cancellationToken)
                       ?? throw NotFoundException.Category(id);

        var expenseCount = await _categories.CountExpensesAsync(id, cancellationToken);

        // Refused rather than cascaded. Deleting a category with history would either
        // take months of expenses with it or leave them pointing at nothing, and
        // neither is something a caller asked for by sending DELETE.
        if (expenseCount > 0)
        {
            throw ConflictException.CategoryInUse(category.Name, expenseCount);
        }

        _categories.Remove(category);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
