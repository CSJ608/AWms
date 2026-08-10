using AWms.Infrastructure.Services;

namespace AWms.Tests.Services;

public class Argon2PasswordHasherTests
{
    [Fact]
    public void HashAndVerify_Success()
    {
        var hasher = new Argon2PasswordHasher();
        var hash = hasher.Hash("MyPassword123");

        Assert.NotNull(hash);
        Assert.NotEmpty(hash);

        Assert.True(hasher.Verify("MyPassword123", hash));
        Assert.False(hasher.Verify("WrongPassword", hash));
    }

    [Fact]
    public void Hash_ReturnsDifferentHashesForSameInput()
    {
        var hasher = new Argon2PasswordHasher();
        var hash1 = hasher.Hash("same");
        var hash2 = hasher.Hash("same");

        Assert.NotEqual(hash1, hash2);
    }
}
