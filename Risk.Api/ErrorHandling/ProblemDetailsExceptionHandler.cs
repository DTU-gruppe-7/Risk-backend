using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Risk.Application.Exceptions;
using Risk.Domain.Exceptions;

namespace Risk.Api.ErrorHandling;

/// <summary>
/// Turns domain and application exceptions into RFC 7807 ProblemDetails.
/// The only place that knows which exception means which HTTP status, so
/// controllers contain no try/catch, and a new exception type is one new line here.
/// </summary>
public sealed class ProblemDetailsExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ValidationFailedException e => new ValidationProblemDetails(e.Errors.ToDictionary())
            {
                Status = StatusCodes.Status400BadRequest,
                Title = e.Message,
            },
            DomainValidationException e => Problem(StatusCodes.Status400BadRequest, "Invalid input.", e.Message),
            BusinessRuleException e => Problem(StatusCodes.Status409Conflict, "The action is not allowed right now.", e.Message),
            ConcurrencyConflictException e => Problem(StatusCodes.Status409Conflict, "Concurrent update.", e.Message),
            NotFoundException e => Problem(StatusCodes.Status404NotFound, "Not found.", e.Message),
            ForbiddenException e => Problem(StatusCodes.Status403Forbidden, "Forbidden.", e.Message),
            AuthenticationFailedException e => Problem(StatusCodes.Status401Unauthorized, "Unauthorized.", e.Message),
            _ => null,
        };

        // Unknown exceptions fall through to the default 500 handling.
        if (problem is null) return false;

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Problem(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };
}
