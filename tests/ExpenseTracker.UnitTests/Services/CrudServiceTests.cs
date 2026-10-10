using ExpenseTracker.Application.DTOs;
using ExpenseTracker.Application.Exceptions;
using ExpenseTracker.Domain.Enums;
using ExpenseTracker.UnitTests.Support;
using ApplicationValidationException = ExpenseTracker.Application.Exceptions.ValidationException;

namespace ExpenseTracker.UnitTests.Services;

public sealed class CategoryServiceTests
{
    [Fact]
    public async Task Create_rejects_a_duplicate_name_ignoring_case()
    {
        using var fixture = new TrackerFixture();

        await fixture.AddCategoryAsync("Groceries");

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => fixture.AddCategoryAsync("groceries"));

        Assert.Contains("groceries", exception.Message);
    }

    [Fact]
    public async Task List_hides_archived_categories_unless_asked()
    {
        using var fixture = new TrackerFixture();

        var groceries = await fixture.AddCategoryAsync("Groceries");
        await fixture.AddCategoryAsync("Transport", "#1565C0");

        await fixture.Categories.UpdateAsync(groceries.Id, new UpdateCategoryRequest { IsArchived = true });

        var visible = await fixture.Categories.ListAsync(includeArchived: false);
        var all = await fixture.Categories.ListAsync(includeArchived: true);

        Assert.Equal(["Transport"], visible.Select(category => category.Name));
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task List_reports_how_many_expenses_each_category_holds()
    {
        using var fixture = new TrackerFixture();

        var groceries = await fixture.AddCategoryAsync("Groceries");
        var transport = await fixture.AddCategoryAsync("Transport", "#1565C0");

        await fixture.AddExpenseAsync(10m, new DateOnly(2026, 10, 1), groceries.Id);
        await fixture.AddExpenseAsync(20m, new DateOnly(2026, 10, 2), groceries.Id);

        var categories = await fixture.Categories.ListAsync(includeArchived: false);

        Assert.Equal(2, categories.Single(category => category.Id == groceries.Id).ExpenseCount);
        Assert.Equal(0, categories.Single(category => category.Id == transport.Id).ExpenseCount);
    }

    [Fact]
    public async Task Delete_is_refused_while_the_category_still_has_expenses()
    {
        using var fixture = new TrackerFixture();

        var groceries = await fixture.AddCategoryAsync("Groceries");
        await fixture.AddExpenseAsync(10m, new DateOnly(2026, 10, 1), groceries.Id);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => fixture.Categories.DeleteAsync(groceries.Id));

        Assert.Contains("Archive it instead", exception.Message);
    }

    [Fact]
    public async Task Delete_succeeds_for_an_unused_category()
    {
        using var fixture = new TrackerFixture();

        var unused = await fixture.AddCategoryAsync("Unused");

        await fixture.Categories.DeleteAsync(unused.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Categories.GetAsync(unused.Id));
    }

    [Fact]
    public async Task Rename_is_refused_when_another_category_already_has_the_name()
    {
        using var fixture = new TrackerFixture();

        await fixture.AddCategoryAsync("Groceries");
        var transport = await fixture.AddCategoryAsync("Transport", "#1565C0");

        await Assert.ThrowsAsync<ConflictException>(() =>
            fixture.Categories.UpdateAsync(transport.Id, new UpdateCategoryRequest { Name = "Groceries" }));
    }

    [Fact]
    public async Task Renaming_a_category_to_its_own_name_is_allowed()
    {
        // The uniqueness check has to exclude the row being edited, or recolouring a
        // category while sending its unchanged name would fail.
        using var fixture = new TrackerFixture();

        var groceries = await fixture.AddCategoryAsync("Groceries");

        var updated = await fixture.Categories.UpdateAsync(
            groceries.Id,
            new UpdateCategoryRequest { Name = "Groceries", ColorHex = "#111111" });

        Assert.Equal("Groceries", updated.Name);
        Assert.Equal("#111111", updated.ColorHex);
    }
}

public sealed class ExpenseServiceTests
{
    [Fact]
    public async Task Create_echoes_the_configured_currency_and_the_category_details()
    {
        using var fixture = new TrackerFixture(options => options.Currency = "EUR");

        var groceries = await fixture.AddCategoryAsync("Groceries");

        var expense = await fixture.AddExpenseAsync(25.50m, new DateOnly(2026, 10, 10), groceries.Id);

        Assert.Equal(25.50m, expense.Amount);
        Assert.Equal("EUR", expense.Currency);
        Assert.Equal("Groceries", expense.CategoryName);
        Assert.Equal("#2E7D32", expense.CategoryColorHex);
    }

    [Fact]
    public async Task Create_reports_an_unknown_category_as_a_field_error()
    {
        // A 400, not a 404: the expense being created is not what is missing, the
        // category the caller pointed at in the body is.
        using var fixture = new TrackerFixture();

        var exception = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => fixture.AddExpenseAsync(10m, new DateOnly(2026, 10, 1), Guid.NewGuid()));

        Assert.True(exception.Errors.ContainsKey(nameof(CreateExpenseRequest.CategoryId)));
    }

    [Fact]
    public async Task Update_leaves_omitted_fields_alone()
    {
        using var fixture = new TrackerFixture();

        var groceries = await fixture.AddCategoryAsync("Groceries");
        var expense = await fixture.AddExpenseAsync(25m, new DateOnly(2026, 10, 10), groceries.Id, "Weekly shop");

        var updated = await fixture.Expenses.UpdateAsync(
            expense.Id,
            new UpdateExpenseRequest { Amount = 31.75m });

        Assert.Equal(31.75m, updated.Amount);
        Assert.Equal("Weekly shop", updated.Description);
        Assert.Equal(new DateOnly(2026, 10, 10), updated.SpentOn);
    }

    [Fact]
    public async Task Update_can_move_an_expense_to_another_category()
    {
        using var fixture = new TrackerFixture();

        var groceries = await fixture.AddCategoryAsync("Groceries");
        var dining = await fixture.AddCategoryAsync("Dining out", "#EF6C00");

        var expense = await fixture.AddExpenseAsync(25m, new DateOnly(2026, 10, 10), groceries.Id);

        var updated = await fixture.Expenses.UpdateAsync(
            expense.Id,
            new UpdateExpenseRequest { CategoryId = dining.Id });

        Assert.Equal(dining.Id, updated.CategoryId);
        Assert.Equal("Dining out", updated.CategoryName);
        Assert.Equal("#EF6C00", updated.CategoryColorHex);
    }

    [Fact]
    public async Task Query_filters_by_date_range_category_method_and_amount()
    {
        using var fixture = new TrackerFixture();

        var groceries = await fixture.AddCategoryAsync("Groceries");
        var transport = await fixture.AddCategoryAsync("Transport", "#1565C0");

        await fixture.AddExpenseAsync(10m, new DateOnly(2026, 9, 28), groceries.Id, "Old shop");
        await fixture.AddExpenseAsync(20m, new DateOnly(2026, 10, 2), groceries.Id, "Weekly shop");
        await fixture.AddExpenseAsync(80m, new DateOnly(2026, 10, 5), groceries.Id, "Big shop", PaymentMethod.CreditCard);
        await fixture.AddExpenseAsync(15m, new DateOnly(2026, 10, 6), transport.Id, "Bus pass");

        var october = new ExpenseQuery { From = new DateOnly(2026, 10, 1), To = new DateOnly(2026, 10, 31) };
        Assert.Equal(3, (await fixture.Expenses.QueryAsync(october)).TotalCount);

        var byCategory = new ExpenseQuery { CategoryId = transport.Id };
        Assert.Equal("Bus pass", Assert.Single((await fixture.Expenses.QueryAsync(byCategory)).Items).Description);

        var byMethod = new ExpenseQuery { PaymentMethod = PaymentMethod.CreditCard };
        Assert.Equal("Big shop", Assert.Single((await fixture.Expenses.QueryAsync(byMethod)).Items).Description);

        var byAmount = new ExpenseQuery { MinAmount = 15m, MaxAmount = 20m };
        Assert.Equal(2, (await fixture.Expenses.QueryAsync(byAmount)).TotalCount);

        var bySearch = new ExpenseQuery { Search = "shop" };
        Assert.Equal(3, (await fixture.Expenses.QueryAsync(bySearch)).TotalCount);
    }

    [Fact]
    public async Task Query_sorts_by_amount_when_asked()
    {
        using var fixture = new TrackerFixture();

        var groceries = await fixture.AddCategoryAsync("Groceries");

        await fixture.AddExpenseAsync(20m, new DateOnly(2026, 10, 2), groceries.Id);
        await fixture.AddExpenseAsync(80m, new DateOnly(2026, 10, 5), groceries.Id);
        await fixture.AddExpenseAsync(10m, new DateOnly(2026, 10, 7), groceries.Id);

        var page = await fixture.Expenses.QueryAsync(new ExpenseQuery { Sort = ExpenseSort.AmountDesc });

        Assert.Equal([80m, 20m, 10m], page.Items.Select(item => item.Amount));
    }

    [Fact]
    public async Task Query_rejects_an_inverted_date_range()
    {
        using var fixture = new TrackerFixture();

        await Assert.ThrowsAsync<ApplicationValidationException>(() =>
            fixture.Expenses.QueryAsync(new ExpenseQuery
            {
                From = new DateOnly(2026, 10, 31),
                To = new DateOnly(2026, 10, 1)
            }));
    }

    [Fact]
    public async Task Query_clamps_an_oversized_page_size()
    {
        using var fixture = new TrackerFixture();

        var groceries = await fixture.AddCategoryAsync("Groceries");
        await fixture.AddExpenseAsync(10m, new DateOnly(2026, 10, 1), groceries.Id);

        var page = await fixture.Expenses.QueryAsync(new ExpenseQuery { PageSize = 5_000 });

        Assert.Equal(ExpenseQuery.MaxPageSize, page.PageSize);
    }

    [Fact]
    public async Task Delete_removes_the_expense()
    {
        using var fixture = new TrackerFixture();

        var groceries = await fixture.AddCategoryAsync("Groceries");
        var expense = await fixture.AddExpenseAsync(10m, new DateOnly(2026, 10, 1), groceries.Id);

        await fixture.Expenses.DeleteAsync(expense.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Expenses.GetAsync(expense.Id));
    }
}

public sealed class BudgetServiceTests
{
    [Fact]
    public async Task Upsert_creates_then_replaces_the_same_budget()
    {
        using var fixture = new TrackerFixture();

        var groceries = await fixture.AddCategoryAsync("Groceries");

        var created = await fixture.SetBudgetAsync(groceries.Id, "2026-10", 400m);
        var replaced = await fixture.SetBudgetAsync(groceries.Id, "2026-10", 450m);

        // Same row, new limit — that is what makes the call idempotent and safe
        // for a caller to repeat.
        Assert.Equal(created.Id, replaced.Id);
        Assert.Equal(450m, replaced.Limit);

        var october = await fixture.Budgets.ListForMonthAsync("2026-10");
        Assert.Single(october);
    }

    [Fact]
    public async Task Budgets_are_scoped_to_their_month()
    {
        using var fixture = new TrackerFixture();

        var groceries = await fixture.AddCategoryAsync("Groceries");

        await fixture.SetBudgetAsync(groceries.Id, "2026-10", 400m);
        await fixture.SetBudgetAsync(groceries.Id, "2026-11", 500m);

        Assert.Equal(400m, Assert.Single(await fixture.Budgets.ListForMonthAsync("2026-10")).Limit);
        Assert.Equal(500m, Assert.Single(await fixture.Budgets.ListForMonthAsync("2026-11")).Limit);
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("October")]
    [InlineData("2026")]
    public async Task A_malformed_month_is_a_field_error(string month)
    {
        using var fixture = new TrackerFixture();

        var exception = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => fixture.Budgets.ListForMonthAsync(month));

        Assert.True(exception.Errors.ContainsKey(nameof(UpsertBudgetRequest.Month)));
    }

    [Fact]
    public async Task Upsert_reports_an_unknown_category_as_a_field_error()
    {
        using var fixture = new TrackerFixture();

        await Assert.ThrowsAsync<ApplicationValidationException>(
            () => fixture.SetBudgetAsync(Guid.NewGuid(), "2026-10", 400m));
    }
}
