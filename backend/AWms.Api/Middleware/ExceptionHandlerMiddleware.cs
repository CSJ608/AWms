using AWms.Domain.Dtos.Common;
using System.Text.Json;

namespace AWms.Api.Middleware;

public class ExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlerMiddleware> _logger;

    public ExceptionHandlerMiddleware(RequestDelegate next, ILogger<ExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Infrastructure.Services.DomainException ex)
        {
            _logger.LogWarning(ex, "Domain exception: {Code}", ex.Code);
            context.Response.StatusCode = ex.StatusCode;
            context.Response.ContentType = "application/json; charset=utf-8";
            var response = ApiResponse.Error<object>(ex.Code, ex.Message);
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json; charset=utf-8";
            var response = ApiResponse.Error<object>("INTERNAL_ERROR", "服务器内部错误");
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
