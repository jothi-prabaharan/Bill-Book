using Identity.Api.Services;
using Identity.Entity.Models;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private const string PreAuthHeader = "X-PreAuth-Token";

    private readonly AuthService _auth;
    private readonly ITokenService _tokens;

    public AuthController(AuthService auth, ITokenService tokens)
    {
        _auth = auth;
        _tokens = tokens;
    }

    /// <summary>Step one: credentials to a pre-auth token plus the accessible organizations.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            LoginResponse response = await _auth.LoginAsync(request, Ip(), UserAgent(), ct);
            return Ok(response);
        }
        catch (AccountLockedException ex)
        {
            return StatusCode(StatusCodes.Status423Locked, new MessageResponse
            {
                Message = $"Account is locked until {ex.Until:u}.",
            });
        }
        catch (InvalidCredentialsException)
        {
            // Generic — never reveal which field was wrong.
            return Unauthorized(new MessageResponse { Message = "Invalid email or password." });
        }
        catch (NoOrganizationAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new MessageResponse
            {
                Message = "This account has no organization access.",
            });
        }
    }

    /// <summary>Step two: exchange the pre-auth token + chosen org for access and refresh tokens.</summary>
    [HttpPost("select-organization")]
    public async Task<IActionResult> SelectOrganization(
        [FromBody] SelectOrganizationRequest request, CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue(PreAuthHeader, out var header) || string.IsNullOrEmpty(header))
        {
            return Unauthorized(new MessageResponse { Message = "Missing pre-auth token." });
        }

        Guid? userId = _tokens.ValidatePreAuthToken(header.ToString());
        if (userId is null)
        {
            return Unauthorized(new MessageResponse { Message = "Invalid or expired pre-auth token." });
        }

        try
        {
            TokenResponse response = await _auth.SelectOrganizationAsync(
                userId.Value, request.OrgId, Ip(), UserAgent(), ct);
            return Ok(response);
        }
        catch (NoOrganizationAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new MessageResponse
            {
                Message = "You do not have access to that organization.",
            });
        }
        catch (DatabaseNotReadyException)
        {
            return StatusCode(StatusCodes.Status409Conflict, new MessageResponse
            {
                Message = "Your account is still being set up. Please try again shortly.",
            });
        }
    }

    /// <summary>Always returns the same 200, whether or not the account exists.</summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await _auth.RequestPasswordResetAsync(request, ct);
        return Ok(new MessageResponse
        {
            Message = "If the account exists, a verification code has been sent.",
        });
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken ct)
    {
        bool ok = await _auth.VerifyOtpAsync(request, ct);
        return ok
            ? Ok(new MessageResponse { Message = "Code verified." })
            : BadRequest(new MessageResponse { Message = "Invalid or expired code." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        bool ok = await _auth.ResetPasswordAsync(request, ct);
        return ok
            ? Ok(new MessageResponse { Message = "Password reset. Please sign in." })
            : BadRequest(new MessageResponse { Message = "Invalid or expired code." });
    }

    private string? Ip() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? UserAgent() => Request.Headers.UserAgent.ToString();
}
