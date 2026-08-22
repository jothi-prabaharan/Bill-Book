using System.Security.Cryptography;

namespace Master.Api.Services;

public interface IOtpService
{
    /// <summary>A fresh 6-digit code plus its SHA-256 hash.</summary>
    (string Code, string Hash) Generate();

    bool Verify(string code, string hash);
}

public sealed class OtpService : IOtpService
{
    public (string Code, string Hash) Generate()
    {
        int value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        string code = value.ToString("D6");
        return (code, HashUtil.Sha256(code));
    }

    public bool Verify(string code, string hash) =>
        CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(HashUtil.Sha256(code)),
            Convert.FromHexString(hash));
}
