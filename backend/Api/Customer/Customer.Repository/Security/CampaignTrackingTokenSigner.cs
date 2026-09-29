using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Customer.Entity.Models;
using Microsoft.Extensions.Configuration;

namespace Customer.Repository.Security;

public interface ICampaignTrackingTokenSigner
{
    string GenerateToken(TrackingTokenPayload payload);
    bool TryValidateToken(string token, out TrackingTokenPayload? payload);
}

public sealed class CampaignTrackingTokenSigner : ICampaignTrackingTokenSigner
{
    private readonly byte[] _key;

    public CampaignTrackingTokenSigner(IConfiguration configuration)
    {
        string? secret = configuration["CampaignTracking:SigningKey"]
            ?? configuration["Unsubscribe:SigningKey"]
            ?? configuration["JwtOptions:SigningKey"]
            ?? "bill-book-campaign-tracking-secret-key-2026";

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    public string GenerateToken(TrackingTokenPayload payload)
    {
        string json = JsonSerializer.Serialize(payload);
        string payloadBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        string signatureBase64 = ComputeSignature(payloadBase64);

        return $"{payloadBase64}.{signatureBase64}";
    }

    public bool TryValidateToken(string token, out TrackingTokenPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        string[] parts = token.Split('.');
        if (parts.Length != 2)
            return false;

        string payloadBase64 = parts[0];
        string signatureBase64 = parts[1];

        string expectedSignature = ComputeSignature(payloadBase64);
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signatureBase64),
            Encoding.UTF8.GetBytes(expectedSignature)))
        {
            return false;
        }

        try
        {
            byte[] bytes = Base64UrlDecode(payloadBase64);
            payload = JsonSerializer.Deserialize<TrackingTokenPayload>(bytes);
            return payload is not null;
        }
        catch
        {
            return false;
        }
    }

    private string ComputeSignature(string payloadBase64)
    {
        using var hmac = new HMACSHA256(_key);
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadBase64));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string text)
    {
        string incoming = text.Replace('-', '+').Replace('_', '/');
        switch (incoming.Length % 4)
        {
            case 2: incoming += "=="; break;
            case 3: incoming += "="; break;
        }
        return Convert.FromBase64String(incoming);
    }
}
