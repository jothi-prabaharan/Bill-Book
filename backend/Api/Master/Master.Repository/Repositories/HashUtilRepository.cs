using Master.Repository.Clients;
using System.Security.Cryptography;
using System.Text;

namespace Master.Repository.Repositories;

/// <summary>
/// SHA-256 hashing for tokens and OTP codes — one-way, we only ever verify.
/// Distinct from password hashing (BCrypt) and from SMTP-password encryption.
/// </summary>
public static class HashUtilRepository
{
    public static string Sha256(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
