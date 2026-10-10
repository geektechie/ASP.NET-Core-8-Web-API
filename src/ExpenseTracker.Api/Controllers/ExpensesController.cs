using Microsoft.AspNetCore.Mvc;
using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.DTOs;
using ExpenseTracker.Application.Interfaces;

namespace ExpenseTracker.Api.Controllers;

/// <summary>Create, find and correct individual expenses.</summary>
[ApiController]
[Route("api/expenses")]
[Produces("application/json")]
public sealed class ExpensesController : ControllerBase
{
    private readonly IExpenseService _service;

    public ExpensesController(IExpenseService service) => _service = service;

    /// <summary>
    /// Lists expenses with optional filters. Dates are inclusive on both ends and
    /// every filter combines with AND.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ExpenseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ExpenseResponse>>> List(
        [FromQuery] ExpenseQuery query,
        CancellationToken cancellationToken) =>
        Ok(await _service.QueryAsync(query, cancellationToken));

    /// <summary>Returns one expense.</summary>
    /// <response code="404">No expense exists with that id.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenseResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await _service.GetAsync(id, cancellationToken));

    /// <summary>Records an expense.</summary>
    /// <response code="201">The expense was recorded.</response>
    /// <response code="400">Invalid amount, date, or an unknown or archived category.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExpenseResponse>> Create(
        [FromBody] CreateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Updates an expense. Omitted fields are left unchanged; send an empty
    /// <c>notes</c> to clear it.
    /// </summary>
    /// <response code="404">No expense exists with that id.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenseResponse>> Update(
        Guid id,
        [FromBody] UpdateExpenseRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _service.UpdateAsync(id, request, cancellationToken));

    /// <summary>Deletes an expense outright.</summary>
    /// <response code="204">The expense was deleted.</response>
    /// <response code="404">No expense exists with that id.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}
