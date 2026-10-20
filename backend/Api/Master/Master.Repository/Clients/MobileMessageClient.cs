using Master.Entity.Enums;
using Microsoft.Extensions.Logging;

namespace Master.Repository.Clients;

/// <summary>A one-time code on its way to a mobile number by WhatsApp or SMS (TK-196).</summary>
/// <param name="Channel">WhatsApp or SMS. Email codes go through <c>IEmailSender</c>, not here.</param>
/// <param name="MobileNumber">In international form, with its leading <c>+</c>: see <see cref="MobileMessages.International"/>.</param>
/// <param name="Code">The six digits, in plain text for this one hop only; only the hash is stored.</param>
/// <param name="Purpose">What the code is for, so a provider can pick the approved template for it.</param>
/// <param name="ValidFor">How long the code works, for the message text.</param>
public sealed record MobileOtpMessage(
    OtpChannel Channel,
    string MobileNumber,
    string Code,
    OtpPurpose Purpose,
    TimeSpan ValidFor);

/// <summary>
/// Sends one-time codes to a mobile number by WhatsApp or SMS: the one place
/// either flow reaches a messaging provider (TK-196).
///
/// <b>No provider is chosen yet (D-66).</b> Which implementation runs is decided
/// in <c>Program.cs</c>, the way <c>IEventPublisher</c> and <c>IFileStorage</c>
/// are: Development writes the code to the log, and anywhere else every send is
/// refused with <see cref="OtpDeliveryException.IsUnavailable"/> set, so a trial
/// signup says it cannot send a code rather than waiting for one that never
/// comes. A provider, once chosen, is a third implementation behind the same
/// interface and nothing else changes.
/// </summary>
public interface IMobileMessageClient
{
    /// <summary>Sends the code, or throws <see cref="OtpDeliveryException"/>.</summary>
    Task SendOtpAsync(MobileOtpMessage message, CancellationToken ct);
}

/// <summary>
/// A code that did not leave. The client logs why before throwing; the caller
/// only decides what the person is told.
/// </summary>
public sealed class OtpDeliveryException : Exception
{
    public OtpDeliveryException(string message, bool isUnavailable, Exception? inner = null)
        : base(message, inner) => IsUnavailable = isUnavailable;

    /// <summary>
    /// True when no provider is configured at all, so trying again or on the
    /// other channel cannot help. False when a configured provider refused or
    /// could not be reached.
    /// </summary>
    public bool IsUnavailable { get; }
}

/// <summary>Helpers every implementation shares.</summary>
public static class MobileMessages
{
    /// <summary>
    /// A stored mobile number in international form, for a provider.
    ///
    /// A local number is stored without its country code and a foreign one with
    /// a leading <c>+</c> (CLAUDE.md, Phone numbers), so a number that starts
    /// with <c>+</c> is already complete and any other takes the country's
    /// <c>PhoneCode</c> (stored as <c>+91</c>). Spaces, dashes and brackets are
    /// dropped; a local number's leading zero (the trunk prefix) goes too. With
    /// no country code known, India's is used, which is every branch today.
    /// </summary>
    public static string International(string mobileNumber, string? countryPhoneCode)
    {
        string trimmed = mobileNumber.Trim();
        string digits = new(trimmed.Where(char.IsAsciiDigit).ToArray());

        if (trimmed.StartsWith('+'))
        {
            return "+" + digits;
        }

        string country = new((countryPhoneCode ?? "+91").Where(char.IsAsciiDigit).ToArray());
        return "+" + country + digits.TrimStart('0');
    }

    /// <summary>The words of the message, the same on either channel.</summary>
    public static string Text(MobileOtpMessage message)
    {
        string what = message.Purpose switch
        {
            OtpPurpose.TrialSignup => "to start your Bill Book trial",
            OtpPurpose.PasswordReset => "to reset your Bill Book password",
            _ => "for Bill Book",
        };

        return $"{message.Code} is your code {what}. It expires in {(int)message.ValidFor.TotalMinutes} minutes. Do not share it with anyone.";
    }
}

/// <summary>
/// Development only: writes the code to the log so a signup or a reset can be
/// finished on a developer's machine. Never registered outside Development,
/// because a code in a log is a code anyone reading the log can use.
/// </summary>
public sealed class LoggingMobileMessageClient : IMobileMessageClient
{
    private readonly ILogger<LoggingMobileMessageClient> _logger;

    public LoggingMobileMessageClient(ILogger<LoggingMobileMessageClient> logger) => _logger = logger;

    public Task SendOtpAsync(MobileOtpMessage message, CancellationToken ct)
    {
        _logger.LogInformation(
            "Development: {Channel} code {Code} for {Purpose} to {MobileNumber}. No provider is configured, so nothing was sent.",
            message.Channel,
            message.Code,
            message.Purpose,
            message.MobileNumber);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Every environment but Development until a provider is chosen (D-66): each
/// send is refused, and logged, so the gap is visible in the logs rather than
/// as people waiting for codes.
/// </summary>
public sealed class UnconfiguredMobileMessageClient : IMobileMessageClient
{
    private readonly ILogger<UnconfiguredMobileMessageClient> _logger;

    public UnconfiguredMobileMessageClient(ILogger<UnconfiguredMobileMessageClient> logger) => _logger = logger;

    public Task SendOtpAsync(MobileOtpMessage message, CancellationToken ct)
    {
        _logger.LogWarning(
            "A {Channel} code for {Purpose} was not sent: no WhatsApp or SMS provider is configured (D-66).",
            message.Channel,
            message.Purpose);

        throw new OtpDeliveryException("No WhatsApp or SMS provider is configured.", isUnavailable: true);
    }
}
