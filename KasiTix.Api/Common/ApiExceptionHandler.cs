using FluentValidation;
using KasiTix.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace KasiTix.Api.Common;

// The ONE place a failure becomes an HTTP status code. No controller action
// contains a try/catch.
public class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad request"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            UnprocessableEntityException => (StatusCodes.Status422UnprocessableEntity, "Unprocessable entity"),
            // TODO (Week 5): two more cases, both 409 Conflict:
            //  1. another request changed the ticket type after we read it (xmin)
            //  2. Postgres rejected a duplicate (SQLSTATE 23505, unique_violation)
            // A switch takes the FIRST pattern that matches. Which of the two
            // has to come first, and why?
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflict"),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }
                => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };
        if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception for {Path}", context.Request.Path);
        }

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message,
                Instance = context.Request.Path
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);
        return true;
    }
}
