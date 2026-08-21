using System.Data;
using System.Text.Json;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Enums;
using AWms.Infrastructure.Data;
using AWms.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace AWms.Api.Middleware;

public class IdempotencyFilter : IAsyncActionFilter
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly IdempotencyService _service;
    private readonly AWmsDbContext _db;
    private readonly ILogger<IdempotencyFilter> _logger;

    public IdempotencyFilter(
        IdempotencyService service,
        AWmsDbContext db,
        ILogger<IdempotencyFilter> logger)
    {
        _service = service;
        _db = db;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        if (method is not ("POST" or "PUT" or "PATCH" or "DELETE"))
        {
            await next();
            return;
        }

        var requiresKey = context.ActionDescriptor.EndpointMetadata.OfType<RequireIdempotencyKeyAttribute>().Any();
        var key = context.HttpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key))
        {
            if (requiresKey)
                context.Result = new BadRequestObjectResult(ApiResponse.Error<object>("VALIDATION_ERROR", "Idempotency-Key 必填"));
            else
                await next();
            return;
        }
        if (key.Length > 128)
        {
            context.Result = new BadRequestObjectResult(ApiResponse.Error<object>("VALIDATION_ERROR", "Idempotency-Key 最长 128"));
            return;
        }

        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                context.HttpContext.RequestAborted);
            await _service.LockKeyAsync(key, context.HttpContext.RequestAborted);

            var reservation = await _service.TryReserveAsync(
                key,
                Ttl,
                preserveCompleted: requiresKey,
                ct: context.HttpContext.RequestAborted);
            if (!reservation.IsFirst)
            {
                await transaction.CommitAsync(CancellationToken.None);
                context.Result = Replay(reservation.Existing!);
                return;
            }

            await transaction.CreateSavepointAsync("business", context.HttpContext.RequestAborted);
            var executed = await next();
            if (executed.Exception is DomainException domainException)
            {
                await transaction.RollbackToSavepointAsync("business", CancellationToken.None);
                _db.ChangeTracker.Clear();

                var json = JsonSerializer.Serialize(
                    ApiResponse.Error<object>(domainException.Code, domainException.Message),
                    ApiJsonOptions.Serializer);
                await _service.CompleteAsync(key, domainException.StatusCode, json, CancellationToken.None);
                await transaction.CommitAsync(CancellationToken.None);

                executed.ExceptionHandled = true;
                executed.Result = new ContentResult
                {
                    StatusCode = domainException.StatusCode,
                    Content = json,
                    ContentType = "application/json; charset=utf-8"
                };
                return;
            }
            if (executed.Exception != null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return;
            }

            var (statusCode, responseJson) = SerializeResult(executed.Result);
            await _service.CompleteAsync(key, statusCode, responseJson, CancellationToken.None);
            await transaction.CommitAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "幂等处理失败（Key={Key}）", key);
            throw;
        }
    }

    private static IActionResult Replay(AWms.Domain.Entities.IdempotencyRecord record)
    {
        if (record.Status != IdempotencyStatus.COMPLETED)
        {
            return new ObjectResult(ApiResponse.Error<object>("CONFLICT", "同 key 写请求处理中，请稍后重试"))
            {
                StatusCode = StatusCodes.Status409Conflict
            };
        }
        if (string.IsNullOrEmpty(record.ResponseJson))
            return new StatusCodeResult(record.StatusCode);

        return new ContentResult
        {
            StatusCode = record.StatusCode,
            Content = record.ResponseJson,
            ContentType = "application/json; charset=utf-8"
        };
    }

    private static (int StatusCode, string ResponseJson) SerializeResult(IActionResult? result)
    {
        if (result is ObjectResult { Value: not null } objectResult)
        {
            return (
                objectResult.StatusCode ?? StatusCodes.Status200OK,
                JsonSerializer.Serialize(objectResult.Value, ApiJsonOptions.Serializer));
        }

        var status = result switch
        {
            StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
            EmptyResult => StatusCodes.Status204NoContent,
            _ => StatusCodes.Status200OK
        };
        return (status, string.Empty);
    }
}
