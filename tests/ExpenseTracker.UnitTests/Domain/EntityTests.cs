using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Domain.Exceptions;
using ExpenseTracker.Domain.ValueObjects;

namespace ExpenseTracker.UnitTests.Domain;

public sealed class CategoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_trims_the_name_and_upper_cases_the_colour()
    {
        var category = Category.Create("  Groceries  ", "#2e7d32", Now);

        Assert.Equal("Groceries", category.Name);

        // Upper-cased so two categories cannot look different in the database while
        // rendering identically in a chart.
        Assert.Equal("#2E7D32", category.ColorHex);
        Assert.False(category.IsArchived);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_blank_name(string name) =>
        Assert.Throws<InvalidTextException>(() => Category.Create(name, "#2E7D32", Now));

    [Theory]
    [InlineData("2E7D32")]
    [InlineData("#2E7D3")]
    [InlineData("#GGGGGG")]
    [InlineData("red")]
    public void Create_rejects_anything_that_is_not_a_six_digit_hex_colour(string colour) =>
        Assert.Throws<InvalidTextException>(() => Category.Create("Groceries", colour, Now));

    [Fact]
    public void EnsureAcceptsNewExpenses_blocks_an_archived_category()
    {
        var category = Category.Create("Groceries", "#2E7D32", Now);

        category.Archive();

        var exception = Assert.Throws<InvalidOperationForStateException>(category.EnsureAcceptsNewExpenses);
        Assert.Contains("archived", exception.Message);

        category.Restore();
        category.EnsureAcceptsNewExpenses();
    }
}

public sealed class ExpenseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly Today = new(2026, 10, 15);

    private static Category Groceries() => Category.Create("Groceries", "#2E7D32", Now);

    private static Expense Sample(decimal amount = 25.50m, DateOnly? spentOn = null) =>
        Expense.Create(
            Money.From(amount),
            spentOn ?? Today,
            Groceries(),
            "Weekly shop",
            PaymentMethod.DebitCard,
            Now);

    [Fact]
    public void Create_stores_the_amount_and_stamps_both_timestamps()
    {
        var expense = Sample();

        Assert.Equal(25.50m, expense.Amount);
        Assert.Equal("Weekly shop", expense.Description);
        Assert.Equal(Now, expense.CreatedAt);
        Assert.Equal(Now, expense.UpdatedAt);
    }

    [Fact]
    public void Create_rejects_a_zero_amount()
    {
        var exception = Assert.Throws<InvalidAmountException>(() => Sample(0m));

        Assert.Contains("greater than zero", exception.Message);
    }

    [Fact]
    public void Create_rejects_a_date_too_far_in_the_future() =>
        Assert.Throws<InvalidDateRangeException>(() => Sample(spentOn: Today.AddDays(2)));

    [Fact]
    public void Create_allows_one_day_of_slack_for_terminal_clocks()
    {
        // A card receipt can carry tomorrow's date when the terminal sits in a
        // timezone ahead of the server, so the boundary is deliberately not zero.
        var expense = Sample(spentOn: Today.AddDays(1));

        Assert.Equal(Today.AddDays(1), expense.SpentOn);
    }

    [Fact]
    public void Create_rejects_an_archived_category()
    {
        var category = Groceries();
        category.Archive();

        Assert.Throws<InvalidOperationForStateException>(() =>
            Expense.Create(Money.From(10m), Today, category, "Shop", PaymentMethod.Cash, Now));
    }

    [Fact]
    public void Create_rejects_a_payment_method_that_is_not_a_defined_value()
    {
        // A cast from an unknown integer is still the enum's type, so without the
        // guard a request carrying paymentMethod: 42 would be stored verbatim.
        Assert.Throws<InvalidTextException>(() =>
            Expense.Create(Money.From(10m), Today, Groceries(), "Shop", (PaymentMethod)42, Now));
    }

    [Fact]
    public void Changing_a_field_moves_UpdatedAt_but_not_CreatedAt()
    {
        var expense = Sample();
        var later = Now.AddHours(3);

        expense.ChangeAmount(Money.From(30m), later);

        Assert.Equal(30m, expense.Amount);
        Assert.Equal(Now, expense.CreatedAt);
        Assert.Equal(later, expense.UpdatedAt);
    }

    [Fact]
    public void ChangeNotes_treats_blank_as_cleared()
    {
        var expense = Sample();

        expense.ChangeNotes("Split with Sam", Now);
        Assert.Equal("Split with Sam", expense.Notes);

        expense.ChangeNotes("   ", Now);
        Assert.Null(expense.Notes);
    }

    [Fact]
    public void ChangeCategory_refuses_to_move_an_expense_into_an_archived_category()
    {
        var expense = Sample();
        var archived = Category.Create("Old", "#111111", Now);
        archived.Archive();

        Assert.Throws<InvalidOperationForStateException>(() => expense.ChangeCategory(archived, Now));
    }
}

public sealed class BudgetTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_requires_a_positive_limit()
    {
        var category = Category.Create("Groceries", "#2E7D32", Now);
        var october = BudgetMonth.Create(2026, 10);

        Assert.Throws<InvalidAmountException>(() =>
            Budget.Create(category, october, Money.Zero, Now));
    }

    [Fact]
    public void An_archived_category_can_still_hold_a_budget()
    {
        // Archiving stops new spending, not the record of what was planned. A past
        // month's budget has to survive so its report still makes sense.
        var category = Category.Create("Groceries", "#2E7D32", Now);
        category.Archive();

        var budget = Budget.Create(category, BudgetMonth.Create(2026, 9), Money.From(400m), Now);

        Assert.Equal(400m, budget.Limit);
    }

    [Fact]
    public void ChangeLimit_updates_the_amount_and_the_timestamp()
    {
        var category = Category.Create("Groceries", "#2E7D32", Now);
        var budget = Budget.Create(category, BudgetMonth.Create(2026, 10), Money.From(400m), Now);
        var later = Now.AddDays(1);

        budget.ChangeLimit(Money.From(450m), later);

        Assert.Equal(450m, budget.Limit);
        Assert.Equal(Now, budget.CreatedAt);
        Assert.Equal(later, budget.UpdatedAt);
    }
}
