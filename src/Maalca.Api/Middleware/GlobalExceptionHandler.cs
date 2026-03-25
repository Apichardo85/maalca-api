using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Api.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, errorCode) = exception switch
        {
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "CONCURRENCY_CONFLICT"),
            InvalidOperationException => (StatusCodes.Status400BadRequest, "INVALID_OPERATION"),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "FORBIDDEN"),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR")
        };

        _logger.LogError(exception, "Unhandled exception: {ErrorCode}", errorCode);

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new
        {
            error = new
            {
                code = errorCode,
                message = statusCode == StatusCodes.Status500InternalServerError
                    ? "An unexpected error occurred"
                    : exception.Message
            }
        }, cancellationToken);

        return true;
    }
}
