using System.Net;
using System.Text.Json;
using Master.Entity.Enums;
using Master.Repository.Clients;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// MSG91 as the WhatsApp and SMS provider for one-time codes (D-66). The request builders and the response
/// reader are pure, so what goes to MSG91 is tested without a network; the sender is tested against a fake
/// handler. Written by an AI and not run (section 0.5 of docs/Task_Details.md). The shapes follow MSG91's v5
/// API as the author knew it, and could not be checked against its documentation from the build environment,
/// so a first run against a real MSG91 account is still owed.
/// </summary>
public class Msg91MobileMessageClientTests
{
    private const string Code = "482913";

    private static Msg91Options Configured() => new()
    {
        AuthKey = "test-auth-key",
        Sms = new Msg91SmsOptions
        {
            TrialSignupTemplateId = "tmpl-signup",
            PasswordResetTemplateId = "tmpl-reset",
        },
        WhatsApp = new Msg91WhatsAppOptions
        {
            IntegratedNumber = "919000000001",
            Namespace = "ns_billbook",
            TrialSignupTemplate = "bb_signup_code",
            PasswordResetTemplate = "bb_reset_code",
        },
    };

    private static MobileOtpMessage Message(
        OtpChannel channel, OtpPurpose purpose = OtpPurpose.TrialSignup, string number = "+919123456780") =>
        new(channel, number, Code, purpose, TimeSpan.FromMinutes(10));

    private static JsonElement Parse(Msg91Request request) => JsonDocument.Parse(request.Json).RootElement;

    // ---- SMS ----

    [Fact]
    public void An_sms_names_the_template_the_number_without_its_plus_and_the_code_under_its_variable()
    {
        Msg91Request request = Msg91Requests.Build(Configured(), Message(OtpChannel.Sms));

        Assert.Equal("api/v5/flow", request.Path);
        JsonElement body = Parse(request);
        Assert.Equal("tmpl-signup", body.GetProperty("template_id").GetString());
        JsonElement recipient = body.GetProperty("recipients")[0];
        Assert.Equal("919123456780", recipient.GetProperty("mobiles").GetString());
        Assert.Equal(Code, recipient.GetProperty("OTP").GetString());
    }

    [Fact]
    public void A_password_reset_sms_uses_the_reset_template()
    {
        Msg91Request request = Msg91Requests.Build(Configured(), Message(OtpChannel.Sms, OtpPurpose.PasswordReset));

        Assert.Equal("tmpl-reset", Parse(request).GetProperty("template_id").GetString());
    }

    [Fact]
    public void The_validity_variable_is_sent_only_when_one_is_configured()
    {
        Msg91Options without = Configured();
        Msg91Options with = Configured();
        with.Sms.ValidityVariable = "MINUTES";

        JsonElement plain = Parse(Msg91Requests.Build(without, Message(OtpChannel.Sms))).GetProperty("recipients")[0];
        JsonElement timed = Parse(Msg91Requests.Build(with, Message(OtpChannel.Sms))).GetProperty("recipients")[0];

        Assert.False(plain.TryGetProperty("MINUTES", out _));
        Assert.Equal("10", timed.GetProperty("MINUTES").GetString());
    }

    [Fact]
    public void The_code_variable_name_is_configurable()
    {
        Msg91Options options = Configured();
        options.Sms.CodeVariable = "VAR1";

        JsonElement recipient = Parse(Msg91Requests.Build(options, Message(OtpChannel.Sms))).GetProperty("recipients")[0];

        Assert.Equal(Code, recipient.GetProperty("VAR1").GetString());
        Assert.False(recipient.TryGetProperty("OTP", out _));
    }

    [Fact]
    public void An_sms_with_no_template_for_its_purpose_is_unavailable()
    {
        Msg91Options options = Configured();
        options.Sms.PasswordResetTemplateId = " ";

        OtpDeliveryException failure = Assert.Throws<OtpDeliveryException>(
            () => Msg91Requests.Build(options, Message(OtpChannel.Sms, OtpPurpose.PasswordReset)));

        Assert.True(failure.IsUnavailable);
        Assert.DoesNotContain(Code, failure.Message);
    }

    // ---- WhatsApp ----

    [Fact]
    public void A_whatsapp_message_names_the_number_the_template_the_language_and_the_code()
    {
        Msg91Request request = Msg91Requests.Build(Configured(), Message(OtpChannel.WhatsApp));

        Assert.Equal("api/v5/whatsapp/whatsapp-outbound-message/bulk/", request.Path);
        JsonElement body = Parse(request);
        Assert.Equal("919000000001", body.GetProperty("integrated_number").GetString());
        Assert.Equal("template", body.GetProperty("content_type").GetString());

        JsonElement template = body.GetProperty("payload").GetProperty("template");
        Assert.Equal("whatsapp", body.GetProperty("payload").GetProperty("messaging_product").GetString());
        Assert.Equal("bb_signup_code", template.GetProperty("name").GetString());
        Assert.Equal("en", template.GetProperty("language").GetProperty("code").GetString());
        Assert.Equal("ns_billbook", template.GetProperty("namespace").GetString());

        JsonElement target = template.GetProperty("to_and_components")[0];
        Assert.Equal("919123456780", target.GetProperty("to")[0].GetString());
        Assert.Equal(Code, target.GetProperty("components").GetProperty("body_1").GetProperty("value").GetString());
    }

    [Fact]
    public void An_authentication_templates_copy_code_button_gets_the_code_too()
    {
        JsonElement components = Parse(Msg91Requests.Build(Configured(), Message(OtpChannel.WhatsApp)))
            .GetProperty("payload").GetProperty("template").GetProperty("to_and_components")[0].GetProperty("components");

        JsonElement button = components.GetProperty("button_1");
        Assert.Equal("url", button.GetProperty("subtype").GetString());
        Assert.Equal(Code, button.GetProperty("value").GetString());
    }

    [Fact]
    public void The_copy_code_button_can_be_switched_off_and_a_blank_namespace_is_left_out()
    {
        Msg91Options options = Configured();
        options.WhatsApp.CopyCodeButton = false;
        options.WhatsApp.Namespace = "";

        JsonElement template = Parse(Msg91Requests.Build(options, Message(OtpChannel.WhatsApp))).GetProperty("payload").GetProperty("template");

        Assert.False(template.TryGetProperty("namespace", out _));
        Assert.False(template.GetProperty("to_and_components")[0].GetProperty("components").TryGetProperty("button_1", out _));
    }

    [Fact]
    public void A_whatsapp_message_with_no_number_or_template_is_unavailable()
    {
        Msg91Options noNumber = Configured();
        noNumber.WhatsApp.IntegratedNumber = null;
        Msg91Options noTemplate = Configured();
        noTemplate.WhatsApp.TrialSignupTemplate = "";

        Assert.True(Assert.Throws<OtpDeliveryException>(() => Msg91Requests.Build(noNumber, Message(OtpChannel.WhatsApp))).IsUnavailable);
        Assert.True(Assert.Throws<OtpDeliveryException>(() => Msg91Requests.Build(noTemplate, Message(OtpChannel.WhatsApp))).IsUnavailable);
    }

    [Fact]
    public void An_email_code_is_not_a_mobile_message()
    {
        OtpDeliveryException failure = Assert.Throws<OtpDeliveryException>(
            () => Msg91Requests.Build(Configured(), Message(OtpChannel.Email)));

        Assert.True(failure.IsUnavailable);
    }

    [Theory]
    [InlineData("+919123456780", "919123456780")]
    [InlineData("+1 (415) 555-0100", "14155550100")]
    [InlineData("919123456780", "919123456780")]
    public void A_recipient_is_the_country_code_and_the_digits(string stored, string expected) =>
        Assert.Equal(expected, Msg91Requests.Recipient(stored));

    // ---- MSG91's answer ----

    [Theory]
    [InlineData(HttpStatusCode.OK, "{\"type\":\"success\",\"message\":\"3763646c\"}", false)]
    [InlineData(HttpStatusCode.OK, "{\"type\":\"error\",\"message\":\"Template not found\"}", true)]
    [InlineData(HttpStatusCode.OK, "{\"type\":\"ERROR\",\"message\":\"Invalid authkey\"}", true)]
    [InlineData(HttpStatusCode.Unauthorized, "{\"type\":\"error\",\"message\":\"Authentication failure\"}", true)]
    [InlineData(HttpStatusCode.InternalServerError, "", true)]
    [InlineData(HttpStatusCode.OK, "", false)]
    [InlineData(HttpStatusCode.OK, "queued", false)]
    public void An_answer_is_a_refusal_when_it_is_not_2xx_or_says_error(HttpStatusCode status, string body, bool refused) =>
        Assert.Equal(refused, Msg91Responses.Failure(status, body) is not null);

    [Fact]
    public void A_provider_text_is_clipped_to_one_line_for_the_log()
    {
        string clipped = Msg91Responses.Clip("line one\nline two\r\n" + new string('x', 500));

        Assert.DoesNotContain('\n', clipped);
        Assert.DoesNotContain('\r', clipped);
        Assert.True(clipped.Length <= 301);
    }

    // ---- The sender ----

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return await respond(request);
        }
    }

    private static (Msg91MobileMessageClient Client, StubHandler Handler) Client(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> respond, Msg91Options? options = null)
    {
        var handler = new StubHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://control.msg91.test/") };
        return (new Msg91MobileMessageClient(http, Options.Create(options ?? Configured()), NullLogger<Msg91MobileMessageClient>.Instance), handler);
    }

    private static Task<HttpResponseMessage> Json(HttpStatusCode status, string body) =>
        Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });

    [Fact]
    public async Task A_code_is_posted_to_msg91_with_the_auth_key_in_a_header_and_not_in_the_url()
    {
        var (client, handler) = Client(_ => Json(HttpStatusCode.OK, "{\"type\":\"success\",\"message\":\"id\"}"));

        await client.SendOtpAsync(Message(OtpChannel.Sms), default);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://control.msg91.test/api/v5/flow", handler.Request.RequestUri!.ToString());
        Assert.Equal("test-auth-key", Assert.Single(handler.Request.Headers.GetValues("authkey")));
        Assert.DoesNotContain("test-auth-key", handler.Request.RequestUri.Query);
        Assert.Contains(Code, handler.Body);
    }

    [Fact]
    public async Task A_refusal_from_msg91_is_a_failed_delivery_that_does_not_name_the_code()
    {
        var (client, _) = Client(_ => Json(HttpStatusCode.OK, "{\"type\":\"error\",\"message\":\"Template not found\"}"));

        OtpDeliveryException failure = await Assert.ThrowsAsync<OtpDeliveryException>(
            () => client.SendOtpAsync(Message(OtpChannel.WhatsApp), default));

        Assert.False(failure.IsUnavailable);
        Assert.DoesNotContain(Code, failure.Message);
    }

    [Fact]
    public async Task An_unreachable_msg91_is_a_failed_delivery_and_is_not_retried()
    {
        var (client, handler) = Client(_ => throw new HttpRequestException("connection refused"));

        OtpDeliveryException failure = await Assert.ThrowsAsync<OtpDeliveryException>(
            () => client.SendOtpAsync(Message(OtpChannel.Sms), default));

        Assert.False(failure.IsUnavailable);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task A_timeout_is_a_failed_delivery()
    {
        var (client, _) = Client(_ => throw new TaskCanceledException("timed out"));

        OtpDeliveryException failure = await Assert.ThrowsAsync<OtpDeliveryException>(
            () => client.SendOtpAsync(Message(OtpChannel.Sms), default));

        Assert.False(failure.IsUnavailable);
    }

    [Fact]
    public async Task Nothing_is_sent_when_there_is_no_auth_key_or_nothing_is_configured_for_the_channel()
    {
        Msg91Options noKey = Configured();
        noKey.AuthKey = " ";
        var (keyless, keylessHandler) = Client(_ => Json(HttpStatusCode.OK, "{}"), noKey);

        Msg91Options noTemplate = Configured();
        noTemplate.Sms.TrialSignupTemplateId = null;
        var (templateless, templatelessHandler) = Client(_ => Json(HttpStatusCode.OK, "{}"), noTemplate);

        Assert.True((await Assert.ThrowsAsync<OtpDeliveryException>(() => keyless.SendOtpAsync(Message(OtpChannel.Sms), default))).IsUnavailable);
        Assert.True((await Assert.ThrowsAsync<OtpDeliveryException>(() => templateless.SendOtpAsync(Message(OtpChannel.Sms), default))).IsUnavailable);
        Assert.Equal(0, keylessHandler.Calls);
        Assert.Equal(0, templatelessHandler.Calls);
    }
}
