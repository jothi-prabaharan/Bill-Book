using Microsoft.EntityFrameworkCore;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;

namespace Master.Api.Services;

/// <summary>
/// Manages the outbound mail account. A platform-wide default row
/// (CustomerId null) is used unless a customer supplies its own mailbox.
///
/// The password is the only reversibly-encrypted secret in the system, because
/// the mail server needs the real value. It is never returned to a client and
/// never logged.
/// </summary>
public sealed class SmtpSettingsService
{
    private readonly MasterDbContext _db;
    private readonly ISecretProtector _protector;

    public SmtpSettingsService(MasterDbContext db, ISecretProtector protector)
    {
        _db = db;
        _protector = protector;
    }

    /// <summary>
    /// The settings a customer would send with: its own row when present,
    /// otherwise the platform default, flagged as inherited.
    /// </summary>
    public async Task<SmtpSettingsDto?> GetAsync(Guid? customerId, CancellationToken ct)
    {
        SmtpSettings? row = customerId is null
            ? await Default(ct)
            : await _db.SmtpSettings.FirstOrDefaultAsync(s => s.CustomerId == customerId, ct);

        bool inherited = false;
        if (row is null && customerId is not null)
        {
            row = await Default(ct);
            inherited = true;
        }

        return row is null ? null : Map(row, inherited);
    }

    public async Task<Guid> SaveAsync(Guid? customerId, SaveSmtpSettingsRequest request, CancellationToken ct)
    {
        SmtpSettings? row = await _db.SmtpSettings
            .FirstOrDefaultAsync(s => s.CustomerId == customerId, ct);

        if (row is null)
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                throw new InvalidOperationException("A password is required when creating SMTP settings.");
            }

            row = new SmtpSettings
            {
                SmtpSettingsId = Guid.NewGuid(),
                CustomerId = customerId,
            };
            _db.SmtpSettings.Add(row);
        }

        row.Host = request.Host;
        row.Port = request.Port;
        row.UseSsl = request.UseSsl;
        row.FromEmail = request.FromEmail;
        row.FromName = request.FromName;
        row.Username = request.Username;
        row.IsActive = request.IsActive;

        // A null password means "leave the stored one alone" — the client never
        // receives it, so it cannot echo it back.
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            row.PasswordEncrypted = _protector.Protect(request.Password);
        }

        await _db.SaveChangesAsync(ct);
        return row.SmtpSettingsId;
    }

    /// <summary>Removes a customer's override so it falls back to the platform default.</summary>
    public async Task<bool> DeleteOverrideAsync(Guid customerId, CancellationToken ct)
    {
        SmtpSettings? row = await _db.SmtpSettings
            .FirstOrDefaultAsync(s => s.CustomerId == customerId, ct);
        if (row is null)
        {
            return false;
        }

        _db.SmtpSettings.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Resolves the settings to send with, decrypting the password. Internal use only.</summary>
    public async Task<ResolvedSmtp?> ResolveAsync(Guid? customerId, CancellationToken ct)
    {
        SmtpSettings? row = customerId is null
            ? null
            : await _db.SmtpSettings.FirstOrDefaultAsync(
                s => s.CustomerId == customerId && s.IsActive, ct);

        row ??= await _db.SmtpSettings.FirstOrDefaultAsync(s => s.CustomerId == null && s.IsActive, ct);
        if (row is null)
        {
            return null;
        }

        return new ResolvedSmtp
        {
            Host = row.Host,
            Port = row.Port,
            UseSsl = row.UseSsl,
            FromEmail = row.FromEmail,
            FromName = row.FromName,
            Username = row.Username,
            Password = _protector.Unprotect(row.PasswordEncrypted),
        };
    }

    private Task<SmtpSettings?> Default(CancellationToken ct) =>
        _db.SmtpSettings.FirstOrDefaultAsync(s => s.CustomerId == null, ct);

    private static SmtpSettingsDto Map(SmtpSettings row, bool inherited) => new()
    {
        SmtpSettingsId = row.SmtpSettingsId,
        CustomerId = row.CustomerId,
        Host = row.Host,
        Port = row.Port,
        UseSsl = row.UseSsl,
        FromEmail = row.FromEmail,
        FromName = row.FromName,
        Username = row.Username,
        HasPassword = !string.IsNullOrEmpty(row.PasswordEncrypted),
        IsActive = row.IsActive,
        IsInherited = inherited,
    };
}

/// <summary>Decrypted SMTP credentials. Never serialized to a client.</summary>
public sealed class ResolvedSmtp
{
    public required string Host { get; init; }

    public required int Port { get; init; }

    public required bool UseSsl { get; init; }

    public required string FromEmail { get; init; }

    public required string FromName { get; init; }

    public required string Username { get; init; }

    public required string Password { get; init; }
}
