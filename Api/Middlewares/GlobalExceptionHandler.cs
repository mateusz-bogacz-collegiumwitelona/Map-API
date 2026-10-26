using Domain.Common;
using Domain.Const;
using Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Api.Middlewares
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellation
            )
        {
            if (exception is OperationCanceledException || httpContext.RequestAborted.IsCancellationRequested)
            {
                _logger.LogInformation("Client cancelled the request: {Method} {Path}",
                    httpContext.Request.Method,
                    httpContext.Request.Path);
                return true;
            }

          

            var (statusCode, message, errorCode) = exception switch
            {
                AppException appEx => (appEx.StatusCode, appEx.Message, appEx.ErrorCode),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "The data has been modified by another user. Please refresh and try again.", ErrorCodes.InvalidOperation),
                KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found.", ErrorCodes.NotFound),
                ArgumentException => (StatusCodes.Status400BadRequest, "Invalid argument provided.", ErrorCodes.BadRequest),
                InvalidOperationException => (StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please contact support.", ErrorCodes.InternalError),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please contact support.", ErrorCodes.InternalError)
            };

            if (exception is AppException appException)
            {
                _logger.LogWarning("Handled application exception: {Message} (Code: {ErrorCode}, Status: {StatusCode})",
                    appException.Message,
                    appException.ErrorCode,
                    appException.StatusCode);
            }
            else
            {
                _logger.LogError(exception, "An unhandled server error occurred: {Message}", exception.Message);
            }

            var result = Result.Failure(
                message: message,
                errorCode: errorCode,
                statusCode: statusCode
            );

            httpContext.Response.StatusCode = statusCode;

            await httpContext.Response.WriteAsJsonAsync(result, cancellation);

            return true;
        }
    }
}