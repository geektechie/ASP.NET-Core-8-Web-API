using ExpenseTracker.Application.DTOs;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Domain.Entities;

namespace ExpenseTracker.Application.Mapping;

/// <summary>
/// Hand-written entity-to-DTO mapping. At this size a mapping library would add a
/// dependency and a layer of reflection to save a few dozen lines.
/// </summary>
public static class DomainMappings
{
    public static ExpenseResponse ToResponse(this ExpenseWithCategory row, string currency) =>
        new(
            row.Expense.Id,
            row.Expense.Amount,
            currency,
            row.Expense.SpentOn,
            row.Expense.CategoryId,
            row.CategoryName,
            row.CategoryColorHex,
            row.Expense.Description,
            row.Expense.PaymentMethod,
            row.Expense.Notes,
            row.Expense.CreatedAt,
            row.Expense.UpdatedAt);

    public static ExpenseResponse ToResponse(this Expense expense, Category category, string currency) =>
        new ExpenseWithCategory(expense, category.Name, category.ColorHex).ToResponse(currency);

    public static CategoryResponse ToResponse(this Category category, int expenseCount) =>
        new(
            category.Id,
            category.Name,
            category.ColorHex,
            category.IsArchived,
            expenseCount,
            category.CreatedAt);

    public static BudgetResponse ToResponse(this BudgetWithCategory row, string currency) =>
        new(
            row.Budget.Id,
            row.Budget.CategoryId,
            row.CategoryName,
            row.CategoryColorHex,
            row.Budget.Month.ToString(),
            row.Budget.Limit,
            currency,
            row.Budget.CreatedAt,
            row.Budget.UpdatedAt);

    public static BudgetResponse ToResponse(this Budget budget, Category category, string currency) =>
        new BudgetWithCategory(budget, category.Name, category.ColorHex).ToResponse(currency);
}
