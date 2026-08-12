using System.Security.Cryptography;
using System.Text;

namespace Master.Api.Services;

/// <summary>
/// Reversible protection for the one secret that must be replayed: the SMTP
/// password. Everything else in the system (login passwords, refresh tokens,
/// OTP codes) is hashed one-way, because we only ever verify those.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string ciphertext);
}

/// <summary>
/// AES-GCM with a key held outside the database. The key comes from
/// configuration in development and from Key Vault in production — never from
/// the same store as the ciphertext, or encrypting it would be pointless.
///
/// Output layout: base64( nonce(12) | tag(16) | ciphertext ).
/// </summary>
public sealed class AesSecretProtector : ISecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public AesSecretProtector(IConfiguration configuration)
    {
        string? configured = configuration["Encryption:Key"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "Encryption:Key is not configured. Set a 32-byte base64 key (Key Vault in production).");
        }

        _key = Convert.FromBase64String(configured);
        if (_key.Length != 32)
        {
            throw new InvalidOperationException("Encryption:Key must decode to exactly 32 bytes (AES-256).");
        }
    }

    public string Protect(string plaintext)
    {
        byte[] plain = Encoding.UTF8.GetBytes(plaintext);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] cipher = new byte[plain.Length];
        byte[] tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        byte[] output = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(output, 0);
        tag.CopyTo(output, NonceSize);
        cipher.CopyTo(output, NonceSize + TagSize);
        return Convert.ToBase64String(output);
    }

    public string Unprotect(string ciphertext)
    {
        byte[] input = Convert.FromBase64String(ciphertext);
        if (input.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Ciphertext is too short to be valid.");
        }

        byte[] nonce = input[..NonceSize];
        byte[] tag = input[NonceSize..(NonceSize + TagSize)];
        byte[] cipher = input[(NonceSize + TagSize)..];
        byte[] plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
