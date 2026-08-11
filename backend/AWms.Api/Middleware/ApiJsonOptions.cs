using System.Text.Encodings.Web;
using System.Text.Json;

namespace AWms.Api.Middleware;

/// <summary>
/// API 响应手写序列化统一选项（契约 2.1：错误 envelope 顶层键小写 code/message/data）。
/// 与 MVC/IdempotencyFilter 输出保持一致：camelCase + 保留非 ASCII 字面量。
/// </summary>
public static class ApiJsonOptions
{
    public static readonly JsonSerializerOptions Serializer = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}