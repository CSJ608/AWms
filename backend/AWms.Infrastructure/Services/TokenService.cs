using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using AWms.Domain.Entities;
using AWms.Domain.Interfaces;

namespace AWms.Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public (string Token, DateTime ExpiresAt) GenerateAccessToken(User user, IReadOnlyList<string> permissions)
    {
        var expiresAt = DateTime.UtcNow.Add(_options.AccessTokenExpiry);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("name", user.Name)
        };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public string GenerateRefreshToken() => Convert.ToBase64String(Guid.CreateVersion7().ToByteArray());

    /// <summary>校验签名（不校验生命周期），用于 refresh 端点接收“过期 token 换新”（契约认证权限 v0.2）。</summary>
    public bool ValidateExpiredToken(string token, out Guid userId, out string username)
    {
        userId = Guid.Empty;
        username = string.Empty;
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
            var validationParams = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                ValidateLifetime = false, // 允许过期 token
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParams, out _);
            var uidClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
            var unameClaim = principal.FindFirst(ClaimTypes.Name);

            if (uidClaim == null || unameClaim == null) return false;
            return Guid.TryParse(uidClaim.Value, out userId) && (username = unameClaim.Value) is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>读取 token 的过期时间（/me 会话恢复用，不重新签发 token）。</summary>
    public DateTime? GetTokenExpiry(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            var exp = jwt.Payload.Expiration;
            return exp.HasValue ? DateTimeOffset.FromUnixTimeSeconds(exp.Value).UtcDateTime : null;
        }
        catch
        {
            return null;
        }
    }

    public class JwtOptions
    {
        public string SecretKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = "AWms";
        public string Audience { get; set; } = "AWms";
        public TimeSpan AccessTokenExpiry { get; set; } = TimeSpan.FromHours(2);
    }
}
