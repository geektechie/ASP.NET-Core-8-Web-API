using Microsoft.AspNetCore.Mvc;
using ExpenseTracker.Application.DTOs;
using ExpenseTracker.Application.Interfaces;

namespace ExpenseTracker.Api.Controllers;

/// <summary>Set monthly spending limits per category.</summary>
[ApiController]
[Route("api/budgets")]
[Produces("application/json")]
public sealed class BudgetsController : ControllerBase
{
    private readonly IBudgetService _service;

    public BudgetsController(IBudgetService service) => _service = service;

    /// <summary>Lists the budgets set for one month.</summary>
    /// <param name="month">The month in <c>yyyy-MM</c> form, e.g. <c>2026-10</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="400">The month is not in <c>yyyy-MM</c> form.</response>
    [HttpGet("{month}")]
    [ProducesResponseType(typeof(IReadOnlyList<BudgetResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<BudgetResponse>>> ListForMonth(
        string month,
        CancellationToken cancellationToken) =>
        Ok(await _service.ListForMonthAsync(month, cancellationToken));

    /// <summary>
    /// Sets the limit for one category in one month, creating it or replacing it.
    /// </summary>
    /// <remarks>
    /// An upsert rather than separate create and update calls: there is only ever one
    /// budget per category and month, so "set the October grocery budget to 400" is a
    /// single idempotent request that a caller can repeat safely.
    /// </remarks>
    /// <response code="200">The limit is now in place.</response>
    /// <response code="400">Unknown category, bad month, or a limit of zero or less.</response>
    [HttpPut]
    [ProducesResponseType(typeof(BudgetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BudgetResponse>> Upsert(
        [FromBody] UpsertBudgetRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _service.UpsertAsync(request, cancellationToken));

    /// <summary>Removes a budget, leaving the category unbudgeted for that month.</summary>
    /// <response code="204">The budget was removed.</response>
    /// <response code="404">No budget exists with that id.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}
