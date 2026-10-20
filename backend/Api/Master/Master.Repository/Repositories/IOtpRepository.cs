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

/// <summary>
/// The limits every one-time code shares, whatever it is for (TK-196). Pure,
/// so the signup and the password reset cannot drift apart.
/// </summary>
public static class OtpPolicy
{
    /// <summary>How long a code works.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>The wait before another code goes to the same place.</summary>
    public static readonly TimeSpan ResendAfter = TimeSpan.FromSeconds(60);

    /// <summary>The window <see cref="MaxPerWindow"/> counts over.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(1);

    /// <summary>
    /// Codes one destination may be sent in <see cref="Window"/>. Each WhatsApp
    /// or SMS message is paid for, and a public form that sends them to any
    /// number is otherwise a way to spend the platform's money on someone
    /// else's behalf.
    /// </summary>
    public const int MaxPerWindow = 5;

    /// <summary>Wrong tries before a code stops working and a new one is needed.</summary>
    public const int MaxAttempts = 5;

    /// <summary>
    /// When a code was sent, from its expiry. The expiry is set from the
    /// service's clock, where the audit columns are not, so this is the
    /// timestamp a test with a fixed clock can reason about.
    /// </summary>
    public static DateTimeOffset SentAt(DateTimeOffset expiresAt) => expiresAt - Lifetime;

    /// <summary>The earliest expiry a code sent inside the current window can have.</summary>
    public static DateTimeOffset WindowExpiryFloor(DateTimeOffset now) => now - Window + Lifetime;

    /// <summary>
    /// How long before another code may go to the same place: zero when one
    /// may go now. Both limits apply, and the longer wait wins.
    /// </summary>
    public static TimeSpan WaitBeforeNext(IEnumerable<DateTimeOffset> sentAt, DateTimeOffset now)
    {
        List<DateTimeOffset> recent = sentAt
            .Where(at => at > now - Window && at <= now)
            .OrderBy(at => at)
            .ToList();

        if (recent.Count == 0)
        {
            return TimeSpan.Zero;
        }

        TimeSpan sinceLast = now - recent[^1];
        TimeSpan wait = sinceLast < ResendAfter ? ResendAfter - sinceLast : TimeSpan.Zero;

        if (recent.Count >= MaxPerWindow)
        {
            // The window frees a slot when the oldest of the last five leaves it.
            TimeSpan untilSlot = recent[^MaxPerWindow] + Window - now;
            if (untilSlot > wait)
            {
                wait = untilSlot;
            }
        }

        return wait;
    }

    /// <summary>A mobile number with all but its last four digits hidden.</summary>
    public static string MaskMobile(string? mobile)
    {
        if (string.IsNullOrEmpty(mobile) || mobile.Length < 4)
        {
            return "****";
        }

        return string.Concat(new string('*', mobile.Length - 4), mobile.AsSpan(mobile.Length - 4));
    }
}
