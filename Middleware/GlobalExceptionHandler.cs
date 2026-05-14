using Grpc.Core;
using Microsoft.AspNetCore.Diagnostics;
using VertexSearchApi.Exceptions;

namespace VertexSearchApi.Middleware;

/// <summary>
/// Translates exceptions to JSON error responses, mirroring the Java ControllerAdvisor.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, message) = exception switch
        {
            InvalidSearchRequestException ex =>
                (StatusCodes.Status400BadRequest, ex.Message),

            BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "Invalid request body"),

            SearchServiceException ex =>
                (StatusCodes.Status503ServiceUnavailable, ex.Message),

            RpcException rpcEx =>
                (MapGrpcStatus(rpcEx.StatusCode), rpcEx.Status.Detail),

            _ =>
                (StatusCodes.Status500InternalServerError, "Internal server error")
        };

        if (statusCode >= 500)
            _logger.LogError(exception, "Unhandled exception");
        else
            _logger.LogWarning(exception, "Client error: {Message}", exception.Message);

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            new { error = message }, cancellationToken: cancellationToken);

        return true;
    }

    private static int MapGrpcStatus(StatusCode code) => code switch
    {
        StatusCode.InvalidArgument   => StatusCodes.Status400BadRequest,
        StatusCode.Unauthenticated   => StatusCodes.Status401Unauthorized,
        StatusCode.PermissionDenied  => StatusCodes.Status403Forbidden,
        StatusCode.NotFound          => StatusCodes.Status404NotFound,
        StatusCode.ResourceExhausted => StatusCodes.Status429TooManyRequests,
        _                            => StatusCodes.Status503ServiceUnavailable
    };
}
