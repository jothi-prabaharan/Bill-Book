using Master.Repository.Clients;

namespace Master.Api.Tests;

/// <summary>
/// Stands in for the WhatsApp and SMS provider (TK-196): records every code it
/// is asked to send, or refuses them all, so a test can read the code a signup
/// or reset sent and check what happens when nothing can be sent.
/// </summary>
public sealed class RecordingMobileMessages : IMobileMessageClient
{
    private readonly OtpDeliveryException? _refusal;

    public RecordingMobileMessages(OtpDeliveryException? refusal = null) => _refusal = refusal;

    public List<MobileOtpMessage> Sent { get; } = [];

    /// <summary>A client that refuses every send as an unconfigured one does.</summary>
    public static RecordingMobileMessages Unavailable() =>
        new(new OtpDeliveryException("No provider.", isUnavailable: true));

    /// <summary>A client whose provider refuses every message.</summary>
    public static RecordingMobileMessages Failing() =>
        new(new OtpDeliveryException("Provider refused.", isUnavailable: false));

    public Task SendOtpAsync(MobileOtpMessage message, CancellationToken ct)
    {
        if (_refusal is not null)
        {
            throw _refusal;
        }

        Sent.Add(message);
        return Task.CompletedTask;
    }
}
