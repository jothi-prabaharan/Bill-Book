using System.Net.Http.Json;
using Shared.Kernel.Interfaces;

namespace Identity.Api.Services;

/// <summary>
/// Sends mail by asking Platform to do it. Platform owns plt.SmtpSettings, so
/// the decrypted password never leaves that service — Identity posts the
/// message, not the credentials.
///
/// Replaces the development LoggingEmailSender.
/// </summary>
public sealed class PlatformEmailSender : IEmailSender
{
    private readonly HttpClient _http;

    public PlatformEmailSender(HttpClient http) => _http = http;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _http.PostAsJsonAsync(
            "internal/notifications/email",
            new
            {
                message.ToEmail,
                message.ToName,
                message.Subject,
                message.HtmlBody,
                message.TextBody,
                message.CustomerId,
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}
