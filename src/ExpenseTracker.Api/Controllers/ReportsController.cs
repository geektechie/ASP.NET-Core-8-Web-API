using Microsoft.AspNetCore.Mvc;
using ExpenseTracker.Application.DTOs;
using ExpenseTracker.Application.Interfaces;

namespace ExpenseTracker.Api.Controllers;

/// <summary>
/// Aggregated views of spending. Every total here is computed by the database, not by
/// loading expenses and adding them up.
/// </summary>
[ApiController]
[Route("api/reports")]
[Produces("application/json")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _service;

    public ReportsController(IReportService service) => _service = service;

    /// <summary>
    /// Totals for a date range, broken down by category and by payment method.
    /// </summary>
    /// <param name="from">Inclusive start. Defaults to the configured window before <paramref name="to"/>.</param>
    /// <param name="to">Inclusive end. Defaults to today.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="400">The range is inverted or longer than the five-year limit.</response>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(SpendingSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SpendingSummaryResponse>> Summary(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken) =>
        Ok(await _service.GetSummaryAsync(from, to, cancellationToken));

    /// <summary>
    /// A spending series over time, ready to plot.
    /// </summary>
    /// <param name="granularity"><c>Daily</c> or <c>Monthly</c>.</param>
    /// <param name="from">Inclusive start.</param>
    /// <param name="to">Inclusive end.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Periods with no spending come back as zero rather than being omitted, so a
    /// chart can plot the points straight through without filling gaps itself.
    /// </remarks>
    [HttpGet("trend")]
    [ProducesResponseType(typeof(SpendingTrendResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SpendingTrendResponse>> Trend(
        [FromQuery] TrendGranularity granularity,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken) =>
        Ok(await _service.GetTrendAsync(from, to, granularity, cancellationToken));

    /// <summary>
    /// Budget versus actual for one month, including a projection while the month is
    /// still running.
    /// </summary>
    /// <param name="month">The month in <c>yyyy-MM</c> form. Defaults to the current month.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Covers every category that has a budget, spending, or both — so spending in a
    /// category nobody budgeted for still shows up.
    /// </remarks>
    [HttpGet("budget-status")]
    [ProducesResponseType(typeof(BudgetStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BudgetStatusResponse>> BudgetStatus(
        [FromQuery] string? month,
        CancellationToken cancellationToken) =>
        Ok(await _service.GetBudgetStatusAsync(month, cancellationToken));
}
