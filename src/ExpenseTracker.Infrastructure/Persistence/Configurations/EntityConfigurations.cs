using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.ValueObjects;
using ExpenseTracker.Infrastructure.Persistence.Converters;

namespace ExpenseTracker.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Name)
            .HasMaxLength(Category.MaxNameLength)
            .IsRequired();

        // Unique under SQL Server's default case-insensitive collation, which is what
        // stops "Groceries" and "groceries" existing side by side.
        builder.HasIndex(category => category.Name)
            .IsUnique()
            .HasDatabaseName("UX_Categories_Name");

        builder.Property(category => category.ColorHex)
            .HasMaxLength(7)
            .IsFixedLength()
            .IsRequired();
    }
}

public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses");

        builder.HasKey(expense => expense.Id);

        builder.Property(expense => expense.Amount)
            // Precision stated explicitly. Left to the provider, a decimal becomes
            // decimal(18,2) on SQL Server anyway, but EF warns about it and the next
            // provider might truncate instead.
            .HasPrecision(18, Money.DecimalPlaces)
            .IsRequired();

        builder.Property(expense => expense.SpentOn)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(expense => expense.Description)
            .HasMaxLength(Expense.MaxDescriptionLength)
            .IsRequired();

        builder.Property(expense => expense.Notes)
            .HasMaxLength(Expense.MaxNotesLength);

        builder.Property(expense => expense.PaymentMethod)
            .HasConversion<int>()
            .IsRequired();

        // Every report filters on a date range first, so SpentOn leads. Amount is
        // included so the summary's SUM and MAX are answered from the index without
        // touching the table.
        builder.HasIndex(expense => expense.SpentOn)
            .IncludeProperties(expense => new { expense.Amount, expense.CategoryId })
            .HasDatabaseName("IX_Expenses_SpentOn");

        // Matches the per-category breakdown, which filters by date within a category.
        builder.HasIndex(expense => new { expense.CategoryId, expense.SpentOn })
            .HasDatabaseName("IX_Expenses_CategoryId_SpentOn");

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(expense => expense.CategoryId)
            // Restrict, not Cascade: deleting a category must never silently take
            // months of spending history with it. The service refuses the delete
            // while expenses exist, and this is the backstop if it ever does not.
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("Budgets");

        builder.HasKey(budget => budget.Id);

        builder.Property(budget => budget.Limit)
            .HasPrecision(18, Money.DecimalPlaces)
            .IsRequired();

        builder.Property(budget => budget.Month)
            .HasConversion<BudgetMonthConverter>()
            .HasColumnType("date")
            .HasColumnName("Month")
            .IsRequired();

        // One budget per category per month. This is what makes the upsert safe: two
        // callers racing to create the same budget cannot both win.
        builder.HasIndex(budget => new { budget.CategoryId, budget.Month })
            .IsUnique()
            .HasDatabaseName("UX_Budgets_CategoryId_Month");

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(budget => budget.CategoryId)
            // Cascade here is safe: a budget is a target, not history. A category
            // with no expenses can be deleted, and its unmet targets go with it.
            .OnDelete(DeleteBehavior.Cascade);
    }
}
