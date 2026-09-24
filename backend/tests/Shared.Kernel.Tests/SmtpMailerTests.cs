using System.Net.Mail;
using Shared.Kernel.Email;
using Shared.Kernel.Interfaces;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// The one place a mail is built (TK-19), shared by Master's in-process sender
/// and the notification worker so the two cannot build it differently.
/// </summary>
public sealed class SmtpMailerTests
{
    private static readonly ResolvedSmtp Mailbox = new()
    {
        Host = "smtp.test",
        Port = 587,
        UseSsl = true,
        FromEmail = "no-reply@shop.in",
        FromName = "Shop",
        Username = "u",
        Password = "p",
    };

    [Fact]
    public void The_message_goes_from_the_mailbox_to_the_recipient_as_html()
    {
        using MailMessage mail = SmtpMailer.Build(Mailbox, new EmailMessage
        {
            ToEmail = "ravi@example.com",
            ToName = "Ravi",
            Subject = "Your code",
            HtmlBody = "<p>123456</p>",
        });

        Assert.Equal("no-reply@shop.in", mail.From!.Address);
        Assert.Equal("Shop", mail.From.DisplayName);
        Assert.Equal("ravi@example.com", Assert.Single(mail.To).Address);
        Assert.Equal("Ravi", mail.To[0].DisplayName);
        Assert.True(mail.IsBodyHtml);
        Assert.Equal("<p>123456</p>", mail.Body);
        Assert.Empty(mail.AlternateViews);
    }

    [Fact]
    public void A_text_body_rides_as_a_plain_alternative_and_a_nameless_recipient_is_addressed_by_email()
    {
        using MailMessage mail = SmtpMailer.Build(Mailbox, new EmailMessage
        {
            ToEmail = "ravi@example.com",
            Subject = "Your code",
            HtmlBody = "<p>123456</p>",
            TextBody = "123456",
        });

        Assert.Equal("ravi@example.com", mail.To[0].DisplayName);
        Assert.Equal("text/plain", Assert.Single(mail.AlternateViews).ContentType.MediaType);
    }
}
