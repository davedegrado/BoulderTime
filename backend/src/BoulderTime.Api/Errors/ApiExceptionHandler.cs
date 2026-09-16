using System.Diagnostics;
using BoulderTime.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Errors;

/// <summary>
/// Single place that turns exceptions into RFC 7807 responses. Every error the API returns has
/// <c>status</c>, <c>title</c>, <c>detail</c>, a stable <c>code</c> and a <c>traceId</c>.
/// </summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        if (exception is OperationCanceledException && http.RequestAborted.IsCancellationRequested)
        {
            http.Response.StatusCode = 499; // client closed request
            return true;
        }

        ProblemDetails problem = exception switch
        {
            // Cast so the switch's natural type is ProblemDetails, not ValidationProblemDetails.
            ValidationException v => (ProblemDetails)new ValidationProblemDetails(v.Errors.ToDictionary(kv => kv.Key, kv => kv.Value))
            {
                Status = StatusCodes.Status400BadRequest, Title = "Invalid request", Detail = v.Message,
            },
            UnauthorizedException => new() { Status = StatusCodes.Status401Unauthorized, Title = "Unauthorized", Detail = exception.Message },
            ForbiddenException => new() { Status = StatusCodes.Status403Forbidden, Title = "Forbidden", Detail = exception.Message },
            NotFoundException => new() { Status = StatusCodes.Status404NotFound, Title = "Not found", Detail = exception.Message },
            ConflictException => new() { Status = StatusCodes.Status409Conflict, Title = "Conflict", Detail = exception.Message },
            UniqueConstraintViolationException => new() { Status = StatusCodes.Status409Conflict, Title = "Conflict", Detail = "This already exists." },
            _ => new() { Status = StatusCodes.Status500InternalServerError, Title = "Server error", Detail = "Something went wrong on our side." },
        };

        problem.Extensions["code"] = exception switch
        {
            AppException app => app.Code,
            UniqueConstraintViolationException => "already_exists",
            _ => "server_error",
        };

        if (problem.Status >= 500)
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", http.Request.Method, http.Request.Path);

        http.Response.StatusCode = problem.Status ?? 500;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem, Exception = exception });
    }

    /// <summary>Adds traceId to every problem response, including framework-generated ones (401, 404, model binding).</summary>
    public static void Customize(ProblemDetailsContext ctx)
    {
        ctx.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
        if (!ctx.ProblemDetails.Extensions.ContainsKey("code"))
        {
            ctx.ProblemDetails.Extensions["code"] = ctx.ProblemDetails.Status switch
            {
                400 => "validation_failed",
                401 => "unauthorized",
                403 => "forbidden",
                404 => "not_found",
                _ => "error",
            };
        }
    }
}
