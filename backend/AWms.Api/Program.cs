using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using AWms.Api.Middleware;
using AWms.Infrastructure;
using AWms.Infrastructure.Data;
using AWms.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// JWT 密钥单一来源（修复 Q6）：优先 Jwt:SecretKey，否则 AWMS_JWT_SECRET；
// 解析后回填到配置，AddInfrastructure（JwtOptions 绑定）与 JwtBearer 共用同一密钥。
var jwtSection = builder.Configuration.GetSection("Jwt");
var secretKey = string.IsNullOrWhiteSpace(jwtSection["SecretKey"])
    ? builder.Configuration["AWMS_JWT_SECRET"]
    : jwtSection["SecretKey"];
if (string.IsNullOrWhiteSpace(secretKey))
    throw new InvalidOperationException("Jwt:SecretKey / AWMS_JWT_SECRET 未配置：密钥禁止写入 git，请通过环境变量或 user-secrets 注入。");
builder.Configuration["Jwt:SecretKey"] = secretKey;
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

// Services
builder.Services.AddControllers(options => options.Filters.Add<AWms.Api.Middleware.IdempotencyFilter>());
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

// JWT Authentication（SecretKey 走环境变量/user-secrets，appsettings 只留占位）
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = key,
        ValidateIssuer = true,
        ValidIssuer = jwtSection["Issuer"] ?? "AWms",
        ValidateAudience = true,
        ValidAudience = jwtSection["Audience"] ?? "AWms",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    // refresh 端点：允许“过期 token”通过认证失败回调，由 AuthService 校验签名后换新（契约认证权限 v0.2）
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api/auth/refresh"))
            {
                context.Response.Headers["X-Auth-Failure"] = "expired";
                context.NoResult();
                return Task.CompletedTask;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// 启动即应用全部迁移（EF Migrations，禁 EnsureCreated）+ 初始管理员初始化（保证新库可登录）
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
    await db.Database.MigrateAsync();
    var adminSeed = scope.ServiceProvider.GetRequiredService<AdminSeedService>();
    await adminSeed.EnsureInitialAdminAsync();
}

// Middleware pipeline：异常处理 → CORS → 认证 → 授权 → 路由/端点
app.UseMiddleware<ExceptionHandlerMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();

public partial class Program { }