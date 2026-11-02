using Master.Entity.Enums;
using Master.Entity.Models;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Email is the default way a code reaches someone, on signup and on a password reset, whichever messaging
/// provider is configured (owner, 2 November 2026). A request that does not name a channel gets an email.
/// Written by an AI and not run (section 0.5 of docs/Task_Details.md).
/// </summary>
public class OtpDefaultChannelTests
{
    [Fact]
    public void A_signup_that_names_no_channel_gets_its_code_by_email() =>
        Assert.Equal(OtpChannel.Email, new SignupRequest().OtpChannel);

    [Fact]
    public void A_password_reset_that_names_no_channel_gets_its_code_by_email() =>
        Assert.Equal(OtpChannel.Email, new ForgotPasswordRequest().Channel);
}
