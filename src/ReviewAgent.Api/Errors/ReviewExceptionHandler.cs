using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ReviewAgent.Application.Reviews;
using ReviewAgent.Domain;

namespace ReviewAgent.Api.Errors;

/// <summary>
/// Turns the exceptions of the review workflow into problem details (RFC 9457) with the right status code.
/// Any other exception is left to the default handler, which answers 500 without details.
/// </summary>
internal sealed class ReviewExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        (int Status, string Title)? problem = exception switch
        {
            DomainException => (StatusCodes.Status400BadRequest, ProblemTitles.RuleViolation),
            ReviewRunNotFoundException => (StatusCodes.Status404NotFound, ProblemTitles.ReviewRunNotFound),
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, ProblemTitles.ConcurrentChange),
            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        var (status, title) = problem.Value;
        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = exception.Message,
            },
        });
    }
}
