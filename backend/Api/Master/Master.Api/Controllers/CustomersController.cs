using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Master.Api.Services;
using Master.Repository.Repositories;
using Master.Repository.Clients;
using Master.Entity.Enums;
using Master.Entity.Models;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Kernel.Persistence;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Master.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/customers")]
[RequireBusinessType(AllowStandalone = true)]
public sealed class CustomersController : ControllerBase
{
    private readonly SignupRepository _signup;

    public CustomersController(SignupRepository signup) => _signup = signup;

    /// <summary>
    /// Public trial signup, in two phases on one route (TK-196).
    ///
    /// <b>Without <c>otpCode</c></b> it checks the whole signup, sends a
    /// six-digit code by email to the signup's address (the default, TK-197)
    /// or on WhatsApp or by SMS to its mobile number, and answers 200 with
    /// where the code went. Nothing is created: no customer, licence, branch,
    /// shard capacity, seed or token.
    ///
    /// <b>With the right <c>otpCode</c></b> it runs the same checks again and
    /// creates the trial, answering 202 with the customer to poll, as before.
    /// One route rather than two means the request that was verified is the
    /// request that creates, with nothing held on the server in between.
    ///
    /// <c>[NoTransaction]</c>: a wrong code's count has to survive the 400 it
    /// answers with, so <c>SignupRepository</c> opens its own transaction for
    /// the writes that must land together.
    /// </summary>
    [HttpPost("signup")]
    [NoTransaction]
    [EnableRateLimiting(OtpRateLimit.Policy)]
    public async Task<IActionResult> Signup([FromBody] SignupRequest request, CancellationToken ct)
    {
        // Public signup is always Standard, whatever it asks for (TK-152).
        request.Kind = CustomerKindRepository.KindToCreate(request.Kind, byOperator: false);
        if (SignupRepository.ScopeRefusal(request.BusinessType, request.AddOns) is string scopeRefusal)
        {
            return BadRequest(new { message = scopeRefusal });
        }

        // The model is shared with a platform operator's create, where the
        // mobile number is optional, so the public signup's own rule is checked
        // here and answered field by field like any other validation error.
        if (request.OtpChannel is OtpChannel.WhatsApp or OtpChannel.Sms
            && string.IsNullOrWhiteSpace(request.MobileNumber))
        {
            ModelState.AddModelError(
                nameof(SignupRequest.MobileNumber), "Mobile number is required to send the code on WhatsApp or by SMS.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (await _signup.EmailExistsAsync(request.Email, ct))
        {
            return BadRequest(new { message = "An account with this email already exists." });
        }

        if (string.IsNullOrEmpty(request.OtpCode))
        {
            return SentResult(await _signup.SendCodeAsync(request, ct), request.OtpChannel);
        }

        try
        {
            TrialSignupResult result = await _signup.SignupWithCodeAsync(request, ct);

            return result.Outcome switch
            {
                TrialSignupOutcome.Created => AcceptedAtAction(
                    nameof(GetStatus), new { customerId = result.Response!.CustomerId }, result.Response),
                TrialSignupOutcome.CodeWrong => BadRequest(new
                {
                    message = result.TriesLeft == 1
                        ? "That code is not right. You have 1 try left."
                        : $"That code is not right. You have {result.TriesLeft} tries left.",
                }),
                TrialSignupOutcome.TooManyTries => BadRequest(new
                {
                    message = "Too many wrong tries. Send a new code and enter that one.",
                }),
                _ => BadRequest(new
                {
                    message = "That code has expired or has already been used. Send a new code.",
                }),
            };
        }
        catch (NoTenantCapacityException)
        {
            // 503, not 500: the request was correct and the platform is out of
            // provisioned database capacity. Retrying once an operator has added
            // a shard will work, and saying so is what stops this reading as a
            // bug in the form the person just filled in. The code is not spent:
            // the refusal rolled that back with everything else.
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Signups are temporarily unavailable while capacity is added. "
                    + "Please try again shortly.",
            });
        }
    }

    /// <summary>The answer to a request for a signup code.</summary>
    private IActionResult SentResult(SignupCodeResult sent, OtpChannel channel) => sent.Outcome switch
    {
        SignupCodeOutcome.Sent => Ok(sent.Response),
        SignupCodeOutcome.TooSoon => StatusCode(StatusCodes.Status429TooManyRequests, new
        {
            message = sent.RetryAfterSeconds > 120
                ? $"Too many codes have gone to this {(channel == OtpChannel.Email ? "address" : "number")}. "
                    + $"Try again in {(sent.RetryAfterSeconds + 59) / 60} minutes."
                : $"A code was sent a moment ago. You can ask for another in {sent.RetryAfterSeconds} seconds.",
            retryAfterSeconds = sent.RetryAfterSeconds,
        }),
        SignupCodeOutcome.DeliveryUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new
        {
            message = channel == OtpChannel.Email
                ? "Codes cannot be sent by email at the moment. Choose WhatsApp or SMS, or try again later."
                : "Codes cannot be sent on WhatsApp or by SMS at the moment. Choose email, or try again later.",
        }),
        _ => StatusCode(StatusCodes.Status502BadGateway, new
        {
            message = channel == OtpChannel.Sms
                ? "The code could not be sent by SMS. Check the mobile number, or choose WhatsApp, and try again."
                : "The code could not be sent on WhatsApp. Check the mobile number, or choose SMS, and try again.",
        }),
    };

    /// <summary>Polled by the signup screen until CanLogin is true.</summary>
    [HttpGet("{customerId:guid}/status")]
    public async Task<IActionResult> GetStatus(Guid customerId, CancellationToken ct)
    {
        CustomerStatusResponse? status = await _signup.GetStatusAsync(customerId, ct);
        return status is null ? NotFound() : Ok(status);
    }
}

