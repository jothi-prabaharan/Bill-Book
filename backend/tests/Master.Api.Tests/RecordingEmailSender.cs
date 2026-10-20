using System.Text.RegularExpressions;
using Shared.Kernel.Interfaces;

namespace Master.Api.Tests;

/// <summary>
/// Stands in for the mail queue (TK-197): records every message it is asked to
/// send, so a test can read the code a signup or reset emailed.
/// </summary>
public sealed partial class RecordingEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }

    /// <summary>The six-digit code in the last message's plain-text body.</summary>
    public string LastCode() => SixDigits().Match(Sent[^1].TextBody!).Value;

    [GeneratedRegex(@"\b\d{6}\b")]
    private static partial Regex SixDigits();
}
