namespace AWms.Infrastructure.Services;

/// <summary>业务异常：契约错误码 + HTTP 状态码，由全局异常中间件统一转 envelope。</summary>
public class DomainException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }

    public DomainException(string code, string message, int statusCode) : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }
}
