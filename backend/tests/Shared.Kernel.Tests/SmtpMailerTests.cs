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

    [Fact]
    public void An_attachment_goes_with_the_message_under_its_name_and_type()
    {
        byte[] workbook = [0x50, 0x4B, 0x03, 0x04];

        using MailMessage mail = SmtpMailer.Build(Mailbox, new EmailMessage
        {
            ToEmail = "ravi@example.com",
            Subject = "Weekly sales",
            HtmlBody = "<p>Attached</p>",
            Attachments =
            [
                new EmailAttachment
                {
                    FileName = "weekly-sales.xlsx",
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    Content = workbook,
                },
            ],
        });

        Attachment attached = Assert.Single(mail.Attachments);
        Assert.Equal("weekly-sales.xlsx", attached.Name);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", attached.ContentType.MediaType);
        using MemoryStream read = new();
        attached.ContentStream.CopyTo(read);
        Assert.Equal(workbook, read.ToArray());
    }

    [Fact]
    public void An_attachment_survives_the_trip_through_the_event()
    {
        byte[] content = [1, 2, 3, 250];
        EmailMessage message = new()
        {
            ToEmail = "ravi@example.com",
            Subject = "Report",
            HtmlBody = "<p>x</p>",
            Attachments = [new EmailAttachment { FileName = "r.xlsx", ContentType = "application/octet-stream", Content = content }],
        };

        EmailAttachment back = Assert.Single(
            Messaging.EmailRequested.From(message, "id", DateTimeOffset.UtcNow).ToMessage().Attachments);

        Assert.Equal("r.xlsx", back.FileName);
        Assert.Equal(content, back.Content);
    }
}
