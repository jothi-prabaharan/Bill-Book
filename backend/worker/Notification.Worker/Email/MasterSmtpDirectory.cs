using System.Net;
using System.Net.Http.Json;
using Shared.Kernel.Email;

namespace Notification.Worker.Email;

/// <summary>The mailbox a customer's mail goes out through.</summary>
public interface ISmtpDirectory
{
    /// <summary>Null when neither the customer nor the platform has one configured.</summary>
    Task<ResolvedSmtp?> ResolveAsync(Guid? customerId, CancellationToken ct);
}

/// <summary>
/// Asks Master, which owns <c>mst.SmtpSettings</c> — the worker may not read
/// another service's tables (hard rule 8). Asked per message and never cached,
/// so the decrypted password is held for one send and a changed mailbox takes
/// effect at once.
/// </summary>
public sealed class MasterSmtpDirectory : ISmtpDirectory
{
    private readonly HttpClient _http;

    public MasterSmtpDirectory(HttpClient http) => _http = http;

    public async Task<ResolvedSmtp?> ResolveAsync(Guid? customerId, CancellationToken ct)
    {
        string url = customerId is Guid id
            ? $"internal/smtp/resolved?customerId={id}"
            : "internal/smtp/resolved";

        using HttpResponseMessage response = await _http.GetAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        // Anything else unexpected is transient as far as the message is
        // concerned: throwing abandons it, and it is delivered again.
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResolvedSmtp>(ct);
    }
}

/// <summary>Puts a message on the wire. An interface so the handler is tested without a mail server.</summary>
public interface IMailTransport
{
    Task SendAsync(ResolvedSmtp smtp, Shared.Kernel.Interfaces.EmailMessage message, CancellationToken ct);
}

public sealed class SmtpMailTransport : IMailTransport
{
    public Task SendAsync(ResolvedSmtp smtp, Shared.Kernel.Interfaces.EmailMessage message, CancellationToken ct) =>
        SmtpMailer.SendAsync(smtp, message, ct);
}
