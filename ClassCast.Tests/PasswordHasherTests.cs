using ClassCast.Common.Security;

namespace ClassCast.Tests;

/// <summary>
/// Verifies the shared-password hashing used by the Teacher Server in workgroup mode.
/// </summary>
public class PasswordHasherTests
{
    [Fact]
    public void Verify_AcceptsCorrectPassword()
    {
        string hash = PasswordHasher.Hash("Sn0wd0nia!");
        Assert.True(PasswordHasher.Verify("Sn0wd0nia!", hash));
    }

    [Fact]
    public void Verify_RejectsWrongPassword()
    {
        string hash = PasswordHasher.Hash("Sn0wd0nia!");
        Assert.False(PasswordHasher.Verify("snowdonia", hash));
    }

    [Fact]
    public void Hash_ProducesDifferentOutputEachTime()
    {
        // A random salt means two hashes of the same password must differ, yet both verify.
        string a = PasswordHasher.Hash("sameword");
        string b = PasswordHasher.Hash("sameword");

        Assert.NotEqual(a, b);
        Assert.True(PasswordHasher.Verify("sameword", a));
        Assert.True(PasswordHasher.Verify("sameword", b));
    }

    [Fact]
    public void Hash_EncodesSchemeAndParameters()
    {
        string hash = PasswordHasher.Hash("anything");
        string[] parts = hash.Split('$');

        Assert.Equal(4, parts.Length);
        Assert.Equal("pbkdf2", parts[0]);
        Assert.Equal("100000", parts[1]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-real-hash")]
    [InlineData("pbkdf2$abc$bad$bad")]
    public void Verify_RejectsEmptyOrMalformedStoredValue(string stored)
    {
        Assert.False(PasswordHasher.Verify("password", stored));
    }

    [Fact]
    public void Verify_RejectsEmptyCandidatePassword()
    {
        string hash = PasswordHasher.Hash("password");
        Assert.False(PasswordHasher.Verify("", hash));
    }

    [Fact]
    public void Hash_EmptyPassword_Throws()
    {
        Assert.Throws<ArgumentException>(() => PasswordHasher.Hash(""));
    }
}
