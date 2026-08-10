using System.Collections.Concurrent;
using System.Text.Json;
using AWms.Domain.Dtos.Common;
using AWms.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AWms.Api.Middleware;

/// <summary>
/// 幂等过滤器：写端点（POST/PUT/PATCH/DELETE）读取 Idempotency-Key（契约 2.6 / 规范 §2.4）。
/// - 同 key 重复请求返回首次结果（含错误响应）；TTL 24h；
/// - 并发同 key：进程内信号量 + 数据库 Key 唯一索引兜底，首个请求先预留。
/// </summary>
public class IdempotencyFilter : IAsyncActionFilter
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> KeyLocks = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);
    private static readonly TimeSpan MaxWaitForFirst = TimeSpan.FromSeconds(15);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IdempotencyService _service;
    private readonly ILogger<IdempotencyFilter> _logger;

    public IdempotencyFilter(IdempotencyService service, ILogger<IdempotencyFilter> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var key = context.HttpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        var method = context.HttpContext.Request.Method;
        if (string.IsNullOrWhiteSpace(key) || method is not ("POST" or "PUT" or "PATCH" or "DELETE"))
        {
            await next();
            return;
        }
        if (key.Length > 128)
        {
            context.Result = new BadRequestObjectResult(ApiResponse.Error<object>("VALIDATION_ERROR", "Idempotency-Key 最长 128"));
            return;
        }

        var sem = KeyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(context.HttpContext.RequestAborted);
        try
        {
            var reservation = await _service.TryReserveAsync(key, Ttl, context.HttpContext.RequestAborted);
            if (!reservation.IsFirst)
            {
                var existing = reservation.Existing;
                if (existing == null)
                {
                    await next();
                    return;
                }

                // 首个请求可能仍在处理（pending）：等待其完成，返回首次结果
                var deadline = DateTime.UtcNow + MaxWaitForFirst;
                while (string.IsNullOrEmpty(existing.ResponseJson) && existing.ExpiresAt > DateTime.UtcNow && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(100, context.HttpContext.RequestAborted);
                    existing = await _service.GetAsync(key, context.HttpContext.RequestAborted) ?? existing;
                }

                if (!string.IsNullOrEmpty(existing.ResponseJson))
                {
                    context.Result = new ContentResult
                    {
                        StatusCode = existing.StatusCode,
                        Content = existing.ResponseJson,
                        ContentType = "application/json; charset=utf-8"
                    };
                    return;
                }

                context.Result = new ObjectResult(ApiResponse.Error<object>("CONFLICT", "同 key 写请求处理中，请稍后重试"))
                {
                    StatusCode = StatusCodes.Status409Conflict
                };
                return;
            }

            // 首个请求：执行并捕获结果（成功与 DomainException 错误都写入记录）
            var executed = await next();

            if (executed.Exception is DomainException dex)
            {
                var json = JsonSerializer.Serialize(ApiResponse.Error<object>(dex.Code, dex.Message), JsonOpts);
                await _service.CompleteAsync(key, dex.StatusCode, json, context.HttpContext.RequestAborted);
                executed.ExceptionHandled = true;
                executed.Result = new ContentResult
                {
                    StatusCode = dex.StatusCode,
                    Content = json,
                    ContentType = "application/json; charset=utf-8"
                };
            }
            else if (executed.Result is ObjectResult { Value: not null } obj)
            {
                var json = JsonSerializer.Serialize(obj.Value, JsonOpts);
                var status = obj.StatusCode ?? StatusCodes.Status200OK;
                await _service.CompleteAsync(key, status, json, context.HttpContext.RequestAborted);
            }
            else
            {
                var status = executed.Result switch
                {
                    StatusCodeResult sc => sc.StatusCode,
                    EmptyResult => StatusCodes.Status204NoContent,
                    _ => StatusCodes.Status200OK
                };
                await _service.CompleteAsync(key, status, string.Empty, context.HttpContext.RequestAborted);
            }
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
        finally
        {
            sem.Release();
        }
    }
}
