
using MassTransit;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Exceptions;
using System.Diagnostics;

namespace OrderFlow.Api.ExceptionHandling;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = exception switch
        {
            DomainValidationException ex => (
                StatusCodes.Status400BadRequest,
                "Validation failed",
                ex.Message),

            BusinessRuleException ex => (
                StatusCodes.Status409Conflict,
                "Business rule violation",
                ex.Message),

            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "Concurrency conflict",
                "The resource was modified by another request."),

            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                "Resource not found",
                "The requested resource could not be found."),

            BadHttpRequestException ex => (
                ex.StatusCode,
                "Invalid HTTP request",
                "The request could not be processed."),

            ArgumentOutOfRangeException ex => (
                StatusCodes.Status400BadRequest,
                "Invalid argument",
                ex.Message),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal server error",
                "An unexpected error occurred.")
        };

        // Log unexpected errors as errors.
        // Expected business-rule failures are warnings.
        if (statusCode >= 500)
        {
            _logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}. TraceId: {TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier);
        }
        else
        {
            _logger.LogWarning(
                "Request failed: {ExceptionType}: {Message}. TraceId: {TraceId}",
                exception.GetType().Name,
                exception.Message,
                httpContext.TraceIdentifier);
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        problem.Extensions["traceId"] =
            Activity.Current?.Id ?? httpContext.TraceIdentifier;

        // Results.Problem produces application/problem+json.
        await Results.Problem(
            problemDetails: problem
        ).ExecuteAsync(httpContext);

        return true;
    }
}
