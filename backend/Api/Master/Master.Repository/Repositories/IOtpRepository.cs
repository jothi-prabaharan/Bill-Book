using Master.Repository.Clients;
using System.Security.Cryptography;

namespace Master.Repository.Repositories;

public interface IOtpRepository
{
    /// <summary>A fresh 6-digit code plus its SHA-256 hash.</summary>
    (string Code, string Hash) Generate();

    bool Verify(string code, string hash);
}

public sealed class OtpService : IOtpRepository
{
    public (string Code, string Hash) Generate()
    {
        int value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        string code = value.ToString("D6");
        return (code, HashUtilRepository.Sha256(code));
    }

    public bool Verify(string code, string hash) =>
        CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(HashUtilRepository.Sha256(code)),
            Convert.FromHexString(hash));
}
