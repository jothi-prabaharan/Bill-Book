using Master.Repository.Repositories;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// The key that protects SMTP passwords (TK-197). From 11 October 2026 the
/// committed key and the code's fallback decoded to 33 bytes, so
/// <see cref="AesSecretProtector"/> threw on construction: no mailbox could be
/// saved and no mail sent, and nothing failed until a mail was due. These read
/// the committed settings themselves, so a bad key fails here instead.
/// </summary>
public sealed class SecretProtectorTests
{
    /// <summary>The <c>backend</c> folder, found by walking up from the test's own output.</summary>
    private static string Backend()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Api", "Master", "Master.Api", "appsettings.json")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find backend/Api/Master/Master.Api from the test output.");
    }

    private static IConfiguration Settings(string file) =>
        new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(Backend(), "Api", "Master", "Master.Api", file), optional: false)
            .Build();

    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    public void The_committed_key_protects_and_recovers_a_password(string file)
    {
        var protector = new AesSecretProtector(Settings(file));

        string protectedText = protector.Protect("an-smtp-password");

        Assert.NotEqual("an-smtp-password", protectedText);
        Assert.Equal("an-smtp-password", protector.Unprotect(protectedText));
    }

    [Fact]
    public void With_no_key_configured_the_fallback_works()
    {
        var protector = new AesSecretProtector(new ConfigurationBuilder().Build());

        Assert.Equal("an-smtp-password", protector.Unprotect(protector.Protect("an-smtp-password")));
    }

    [Fact]
    public void A_key_of_the_wrong_length_is_refused()
    {
        IConfiguration wrong = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Key"] = Convert.ToBase64String(new byte[33]),
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => new AesSecretProtector(wrong));
    }
}
