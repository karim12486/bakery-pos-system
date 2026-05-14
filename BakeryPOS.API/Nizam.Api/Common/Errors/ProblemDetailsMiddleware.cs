using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Common.Errors;

/// <summary>
/// Translates uncaught exceptions into RFC 7807 ProblemDetails responses with a stable
/// <c>errorCode</c> extension. Handles:
/// <list type="bullet">
///   <item><see cref="DomainException"/> → status from the exception, message + errorCode in body</item>
///   <item><see cref="ValidationException"/> (FluentValidation) → 422 with field-level errors</item>
///   <item>Anything else → 500 with a generic message (full detail goes to logs only)</item>
/// </list>
/// </summary>
public sealed class ProblemDetailsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ProblemDetailsMiddleware(RequestDelegate next, ILogger<ProblemDetailsMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (DomainException ex)
        {
            _logger.LogInformation(ex, "Domain exception {ErrorCode}: {Message}", ex.ErrorCode, ex.Message);
            await WriteProblem(ctx, ex.StatusCode, title: TitleForStatus(ex.StatusCode), detail: ex.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = ex.ErrorCode });
        }
        catch (ValidationException ex)
        {
            _logger.LogInformation(ex, "Validation failure on {Path}", ctx.Request.Path);
            var errors = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            await WriteProblem(ctx, StatusCodes.Status422UnprocessableEntity,
                title: "One or more validation errors occurred.",
                detail: "See 'errors' for field-level details.",
                extensions: new Dictionary<string, object?>
                {
                    ["errorCode"] = "ERR_VALIDATION",
                    ["errors"] = errors
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Path}", ctx.Request.Path);
            await WriteProblem(ctx, StatusCodes.Status500InternalServerError,
                title: "An unexpected error occurred.",
                detail: _env.IsDevelopment() ? ex.ToString() : "Please contact support if this persists.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "ERR_INTERNAL" });
        }
    }

    private static async Task WriteProblem(
        HttpContext ctx, int status, string title, string detail,
        IDictionary<string, object?>? extensions = null)
    {
        if (ctx.Response.HasStarted) return;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = $"https://httpstatuses.io/{status}",
            Instance = ctx.Request.Path
        };
        if (extensions != null)
        {
            foreach (var (k, v) in extensions) problem.Extensions[k] = v;
        }
        problem.Extensions["traceId"] = ctx.TraceIdentifier;

        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        await JsonSerializer.SerializeAsync(ctx.Response.Body, problem,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private static string TitleForStatus(int status) => status switch
    {
        400 => "Bad request.",
        401 => "Unauthorized.",
        403 => "Forbidden.",
        404 => "Not found.",
        409 => "Conflict.",
        422 => "Unprocessable entity.",
        _ => "An error occurred."
    };
}
