namespace AWms.Api.Middleware;

[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireIdempotencyKeyAttribute : Attribute;
