using FluentValidation;
using JbNet.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace JbNet.Api.Middleware;

public sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            var errors = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            var problem = new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Validation failed.",
                Type = "https://tools.ietf.org/html/rfc7807"
            };
            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            await context.Response.WriteAsJsonAsync(problem);
        }
        catch (DomainException ex) when (ex is ActiveRequestLimitExceededException or CooldownActiveException)
        {
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Business rule violation.", ex.Message);
        }
        catch (DomainException ex) when (ex is UnauthorizedHopActionException)
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "Forbidden.", ex.Message);
        }
        catch (DomainException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Domain rule violation.", ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "Resource not found.", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.", "Please try again later.");
        }
    }

    private static Task WriteProblemAsync(HttpContext context, int status, string title, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = "https://tools.ietf.org/html/rfc7807"
        };
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(problem);
    }
}
