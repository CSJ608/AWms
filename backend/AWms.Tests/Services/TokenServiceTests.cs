using System.IdentityModel.Tokens.Jwt;
using AWms.Domain.Entities;
using AWms.Domain.Enums;
using AWms.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace AWms.Tests.Services;

public class TokenServiceTests
{
    private static TokenService CreateTokenService(TimeSpan expiry) =>
        new(Options.Create(new TokenService.JwtOptions
        {
            SecretKey = "TestSecretKey-MustBeAtLeast32Chars!",
            Issuer = "AWms",
            Audience = "AWms",
            AccessTokenExpiry = expiry
        }));

    [Fact]
    public void GenerateAccessToken_含用户身份与权限claims()
    {
        var service = CreateTokenService(TimeSpan.FromHours(2));
        var user = new User { Id = Guid.NewGuid(), Username = "wang01", Name = "王仓管", Status = UserStatus.ACTIVE };

        var (token, expiresAt) = service.GenerateAccessToken(user, new List<string> { "route.inbound", "action.receiving.create" });

        Assert.NotEmpty(token);
        Assert.True(expiresAt > DateTime.UtcNow);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        Assert.Contains(jwt.Claims, c => c.Value == user.Id.ToString() && (c.Type == "nameid" || c.Type.EndsWith("nameidentifier")));
        Assert.Contains(jwt.Claims, c => c.Value == "wang01" && (c.Type == "unique_name" || c.Type.EndsWith("/name")));
        Assert.Contains(jwt.Claims, c => c.Type == "permission" && c.Value == "route.inbound");
    }

    [Fact]
    public void ValidateExpiredToken_真实过期token_可解析身份()
    {
        var service = CreateTokenService(TimeSpan.FromHours(-2));
        var user = new User { Id = Guid.NewGuid(), Username = "wang01", Name = "王仓管", Status = UserStatus.ACTIVE };
        var (token, expiresAt) = service.GenerateAccessToken(user, new List<string>());
        Assert.True(expiresAt < DateTime.UtcNow);

        var ok = service.ValidateExpiredToken(token, out var userId, out var username);

        Assert.True(ok);
        Assert.Equal(user.Id, userId);
        Assert.Equal("wang01", username);
    }

    [Fact]
    public void ValidateExpiredToken_伪造token_返回false()
    {
        var service = CreateTokenService(TimeSpan.FromHours(2));

        var ok = service.ValidateExpiredToken("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.fake", out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void GetTokenExpiry_返回token过期时间()
    {
        var service = CreateTokenService(TimeSpan.FromHours(2));
        var user = new User { Id = Guid.NewGuid(), Username = "u", Name = "n", Status = UserStatus.ACTIVE };
        var (token, expiresAt) = service.GenerateAccessToken(user, new List<string>());

        var expiry = service.GetTokenExpiry(token);

        Assert.NotNull(expiry);
        Assert.True(Math.Abs((expiry.Value - expiresAt).TotalSeconds) < 5);
    }
}

