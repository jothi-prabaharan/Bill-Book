using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Master.Entity.Enums;
using Master.Repository.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Master.Repository.Clients;

/// <summary>
/// MSG91 as the WhatsApp and SMS provider for one-time codes (D-66, chosen by the owner on 2 November 2026).
/// Bound from the <c>Msg91</c> configuration section. The auth key is a secret: it comes from the environment
/// or Key Vault (<c>Msg91__AuthKey</c>, or <c>Msg91--AuthKey</c> in Key Vault) and is never committed.
/// Email stays the default channel for both flows; this client only ever sees a code the person asked to have
/// sent by WhatsApp or SMS.
/// </summary>
public sealed class Msg91Options
{
    public const string Section = "Msg91";

    /// <summary>The account's auth key. When it is blank MSG91 is not configured and this client is not used.</summary>
    public string? AuthKey { get; set; }

    /// <summary>MSG91's API host. Always <c>https</c> outside Development, because the auth key travels in a header.</summary>
    public string BaseUrl { get; set; } = "https://control.msg91.com/";

    public Msg91SmsOptions Sms { get; set; } = new();

    public Msg91WhatsAppOptions WhatsApp { get; set; } = new();
}

/// <summary>
/// SMS goes through MSG91's Flow API with a DLT-registered template. The owner registers one template per
/// purpose (the wording is in the user guide), and the template's variable names are set here because MSG91
/// fills a template by name.
/// </summary>
public sealed class Msg91SmsOptions
{
    /// <summary>The MSG91 template id for a trial signup code.</summary>
    public string? TrialSignupTemplateId { get; set; }

    /// <summary>The MSG91 template id for a password reset code.</summary>
    public string? PasswordResetTemplateId { get; set; }

    /// <summary>The template variable that holds the six digits.</summary>
    public string CodeVariable { get; set; } = "OTP";

    /// <summary>
    /// The template variable that holds how many minutes the code lasts, for a template whose text says so.
    /// Blank sends none, so a template without it is not given a variable it does not have.
    /// </summary>
    public string? ValidityVariable { get; set; }
}

/// <summary>
/// WhatsApp goes through MSG91's outbound template message with an approved authentication template.
/// </summary>
public sealed class Msg91WhatsAppOptions
{
    /// <summary>The WhatsApp business number connected to MSG91, with its country code and no <c>+</c>.</summary>
    public string? IntegratedNumber { get; set; }

    /// <summary>The namespace MSG91 shows for the account's templates. Blank leaves it out.</summary>
    public string? Namespace { get; set; }

    /// <summary>The approved template's language code.</summary>
    public string Language { get; set; } = "en";

    /// <summary>The approved template's name for a trial signup code.</summary>
    public string? TrialSignupTemplate { get; set; }

    /// <summary>The approved template's name for a password reset code.</summary>
    public string? PasswordResetTemplate { get; set; }

    /// <summary>
    /// An authentication template carries a "Copy code" button, and WhatsApp wants the code again for it. On by
    /// default because every authentication template has the button; off for a plain utility template without one.
    /// </summary>
    public bool CopyCodeButton { get; set; } = true;
}

/// <summary>A request ready to post to MSG91: its path under the base address, and its JSON body.</summary>
public sealed record Msg91Request(string Path, string Json);

/// <summary>
/// What each channel sends to MSG91, built without a network so the shapes can be tested. A missing setting is
/// an <see cref="OtpDeliveryException"/> with <see cref="OtpDeliveryException.IsUnavailable"/> set: nothing is
/// configured for that channel and purpose, so trying again cannot help.
/// </summary>
public static class Msg91Requests
{
    /// <summary>MSG91's Flow API, which sends a registered SMS template.</summary>
    public const string SmsPath = "api/v5/flow";

    /// <summary>MSG91's outbound WhatsApp template message.</summary>
    public const string WhatsAppPath = "api/v5/whatsapp/whatsapp-outbound-message/bulk/";

    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static Msg91Request Build(Msg91Options options, MobileOtpMessage message)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(message);

        return message.Channel switch
        {
            OtpChannel.Sms => Sms(options, message),
            OtpChannel.WhatsApp => WhatsApp(options, message),
            _ => throw new OtpDeliveryException(
                $"{message.Channel} codes do not go through a mobile messaging provider.", isUnavailable: true),
        };
    }

    public static Msg91Request Sms(Msg91Options options, MobileOtpMessage message)
    {
        string? templateId = message.Purpose switch
        {
            OtpPurpose.TrialSignup => options.Sms.TrialSignupTemplateId,
            OtpPurpose.PasswordReset => options.Sms.PasswordResetTemplateId,
            _ => null,
        };

        if (string.IsNullOrWhiteSpace(templateId))
        {
            throw new OtpDeliveryException(
                $"MSG91 has no SMS template configured for {message.Purpose}.", isUnavailable: true);
        }

        var recipient = new Dictionary<string, object>
        {
            ["mobiles"] = Recipient(message.MobileNumber),
            [options.Sms.CodeVariable] = message.Code,
        };

        if (!string.IsNullOrWhiteSpace(options.Sms.ValidityVariable))
        {
            recipient[options.Sms.ValidityVariable] = ((int)message.ValidFor.TotalMinutes).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return new Msg91Request(SmsPath, JsonSerializer.Serialize(
            new { template_id = templateId.Trim(), short_url = "0", recipients = new[] { recipient } }, Json));
    }

    public static Msg91Request WhatsApp(Msg91Options options, MobileOtpMessage message)
    {
        Msg91WhatsAppOptions whatsApp = options.WhatsApp;

        string? templateName = message.Purpose switch
        {
            OtpPurpose.TrialSignup => whatsApp.TrialSignupTemplate,
            OtpPurpose.PasswordReset => whatsApp.PasswordResetTemplate,
            _ => null,
        };

        if (string.IsNullOrWhiteSpace(whatsApp.IntegratedNumber) || string.IsNullOrWhiteSpace(templateName))
        {
            throw new OtpDeliveryException(
                $"MSG91 has no WhatsApp number or template configured for {message.Purpose}.", isUnavailable: true);
        }

        var components = new Dictionary<string, object>
        {
            ["body_1"] = new { type = "text", value = message.Code },
        };

        if (whatsApp.CopyCodeButton)
        {
            components["button_1"] = new { subtype = "url", type = "text", value = message.Code };
        }

        var body = new
        {
            integrated_number = Recipient(whatsApp.IntegratedNumber),
            content_type = "template",
            payload = new
            {
                messaging_product = "whatsapp",
                type = "template",
                template = new
                {
                    name = templateName.Trim(),
                    language = new { code = whatsApp.Language, policy = "deterministic" },
                    @namespace = string.IsNullOrWhiteSpace(whatsApp.Namespace) ? null : whatsApp.Namespace.Trim(),
                    to_and_components = new[]
                    {
                        new { to = new[] { Recipient(message.MobileNumber) }, components },
                    },
                },
            },
        };

        return new Msg91Request(WhatsAppPath, JsonSerializer.Serialize(body, Json));
    }

    /// <summary>
    /// A number as MSG91 takes it: the country code and the digits, with no <c>+</c>, spaces or dashes. The
    /// stored number is already international (<see cref="MobileMessages.International"/>).
    /// </summary>
    public static string Recipient(string internationalNumber) =>
        new(internationalNumber.Where(char.IsAsciiDigit).ToArray());
}

/// <summary>Reading MSG91's answer, without a network so it can be tested.</summary>
public static class Msg91Responses
{
    private const int BodyLogLimit = 300;

    /// <summary>
    /// Why MSG91 did not take the message, or null when it did. A refusal is a non-2xx answer or a 2xx whose
    /// body says <c>"type": "error"</c>, which is how MSG91 reports a bad template or number. Anything else is
    /// taken as accepted: a body that cannot be read is not evidence that nothing was sent, and treating it as a
    /// failure would tell the person no code was sent when one may be on its way.
    /// </summary>
    public static string? Failure(HttpStatusCode status, string? body)
    {
        if ((int)status is < 200 or > 299)
        {
            return $"MSG91 answered {(int)status}: {Clip(body)}";
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("type", out JsonElement type)
                && type.ValueKind == JsonValueKind.String
                && string.Equals(type.GetString(), "error", StringComparison.OrdinalIgnoreCase))
            {
                string reason = root.TryGetProperty("message", out JsonElement message) && message.ValueKind == JsonValueKind.String
                    ? message.GetString() ?? string.Empty
                    : body;

                return $"MSG91 refused it: {Clip(reason)}";
            }
        }
        catch (JsonException)
        {
            // Not JSON on a 2xx: see the summary.
        }

        return null;
    }

    /// <summary>The start of a provider's text, on one line, for a log.</summary>
    public static string Clip(string? text)
    {
        string oneLine = new((text ?? string.Empty).Select(c => char.IsControl(c) ? ' ' : c).ToArray());
        return oneLine.Length <= BodyLogLimit ? oneLine : oneLine[..BodyLogLimit] + "…";
    }
}

/// <summary>
/// Sends a one-time code by WhatsApp or SMS through MSG91. <b>It never retries</b>: a second attempt after a
/// timeout could deliver two codes, and the person can ask for another. Nothing it logs holds the code or the
/// auth key, and the number is masked.
/// </summary>
public sealed class Msg91MobileMessageClient : IMobileMessageClient
{
    private readonly HttpClient _http;
    private readonly IOptions<Msg91Options> _options;
    private readonly ILogger<Msg91MobileMessageClient> _logger;

    public Msg91MobileMessageClient(HttpClient http, IOptions<Msg91Options> options, ILogger<Msg91MobileMessageClient> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public async Task SendOtpAsync(MobileOtpMessage message, CancellationToken ct)
    {
        Msg91Options options = _options.Value;

        if (string.IsNullOrWhiteSpace(options.AuthKey))
        {
            _logger.LogWarning("A {Channel} code for {Purpose} was not sent: no MSG91 auth key is configured.", message.Channel, message.Purpose);
            throw new OtpDeliveryException("No MSG91 auth key is configured.", isUnavailable: true);
        }

        Msg91Request built;
        try
        {
            built = Msg91Requests.Build(options, message);
        }
        catch (OtpDeliveryException unavailable)
        {
            _logger.LogWarning("A {Channel} code for {Purpose} was not sent: {Reason}", message.Channel, message.Purpose, unavailable.Message);
            throw;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, built.Path)
        {
            Content = new StringContent(built.Json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("authkey", options.AuthKey.Trim());
        request.Headers.Accept.ParseAdd("application/json");

        string? failure;
        try
        {
            using HttpResponseMessage response = await _http.SendAsync(request, ct);
            string body = await response.Content.ReadAsStringAsync(ct);
            failure = Msg91Responses.Failure(response.StatusCode, body);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "A {Channel} code for {Purpose} to {Mobile} was not sent: MSG91 could not be reached.",
                message.Channel, message.Purpose, OtpPolicy.MaskMobile(message.MobileNumber));
            throw new OtpDeliveryException("MSG91 could not be reached.", isUnavailable: false, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "A {Channel} code for {Purpose} to {Mobile} was not sent: MSG91 did not answer in time.",
                message.Channel, message.Purpose, OtpPolicy.MaskMobile(message.MobileNumber));
            throw new OtpDeliveryException("MSG91 did not answer in time.", isUnavailable: false, ex);
        }

        if (failure is not null)
        {
            _logger.LogError("A {Channel} code for {Purpose} to {Mobile} was not sent. {Failure}",
                message.Channel, message.Purpose, OtpPolicy.MaskMobile(message.MobileNumber), failure);
            throw new OtpDeliveryException("MSG91 did not accept the message.", isUnavailable: false);
        }
    }
}
