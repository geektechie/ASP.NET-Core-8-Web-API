using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ExpenseTracker.Application.Exceptions;
using ExpenseTracker.Domain.Exceptions;
using ApplicationValidationException = ExpenseTracker.Application.Exceptions.ValidationException;

namespace ExpenseTracker.Api.Infrastructure;

/// <summary>
/// Turns every unhandled exception into RFC 7807 <c>application/problem+json</c>, so
/// clients get one error shape from the whole API instead of a mix of stack traces
/// and ad-hoc JSON.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        IProblemDetailsService problemDetailsService,
        IHostEnvironment environment,
        ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _environment = environment;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // The client gave up; there is nobody left to send a response to.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        var problemDetails = Describe(exception);

        problemDetails.Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}";
        problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        if (problemDetails.Status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception on {Path}.", httpContext.Request.Path);
        }
        else
        {
            _logger.LogInformation(
                "Request on {Path} failed with {Status}: {Message}",
                httpContext.Request.Path,
                problemDetails.Status,
                exception.Message);
        }

        httpContext.Response.StatusCode = problemDetails.Status!.Value;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }

    private ProblemDetails Describe(Exception exception) => exception switch
    {
        ApplicationValidationException validation => new ValidationProblemDetails(
            validation.Errors.ToDictionary(entry => entry.Key, entry => entry.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred."
        },

        // Domain messages are written for humans ("An expense must be greater than
        // zero"), so they are safe and useful to return verbatim.
        DomainException domain => new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "The request is invalid.",
            Detail = domain.Message
        },

        NotFoundException notFound => new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Resource not found.",
            Detail = notFound.Message
        },

        ConflictException conflict => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "The request conflicts with existing data.",
            Detail = conflict.Message
        },

        _ => new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",

            // Anything unrecognised may carry connection strings or file paths in its
            // message, so it is only echoed outside production.
            Detail = _environment.IsDevelopment() ? exception.ToString() : null
        }
    };
}
