using Identity.Api.Services;
using Identity.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers;

[ApiController]
[AllowAnonymous]
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

    /// <summary>
    /// The branches the signed-in user may work in. The same list login offers,
    /// read again so a switcher never has to cache it.
    /// </summary>
    [Authorize]
    [HttpGet("organizations")]
    public async Task<IActionResult> Organizations(CancellationToken ct) =>
        CallerUserId() is Guid userId
            ? Ok(await _auth.AccessibleOrgsAsync(userId, ct))
            : Unauthorized();

    /// <summary>
    /// Moves an already signed-in user to another branch.
    ///
    /// Reuses the same path as step two of login: a branch is a separate set of
    /// books, so switching into one means a new token carrying that org and the
    /// permissions the user holds *there*. Re-authenticating is not required —
    /// the caller has already proved who they are — but access to the target
    /// branch is checked exactly as it is at login.
    /// </summary>
    [Authorize]
    [HttpPost("switch-organization")]
    public async Task<IActionResult> SwitchOrganization(
        [FromBody] SelectOrganizationRequest request, CancellationToken ct)
    {
        if (CallerUserId() is not Guid userId)
        {
            return Unauthorized();
        }

        try
        {
            TokenResponse response = await _auth.SelectOrganizationAsync(
                userId, request.OrgId, Ip(), UserAgent(), ct);

            return Ok(response);
        }
        catch (NoOrganizationAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new MessageResponse
            {
                Message = "You do not have access to that branch.",
            });
        }
        catch (DatabaseNotReadyException)
        {
            return StatusCode(StatusCodes.Status409Conflict, new MessageResponse
            {
                Message = "That branch is still being set up. Please try again shortly.",
            });
        }
    }

    /// <summary>The user on the access token, for endpoints that need no pre-auth step.</summary>
    private Guid? CallerUserId() =>
        Guid.TryParse(
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value,
            out Guid userId)
            ? userId
            : null;

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

    /// <summary>Completes an invitation from the emailed link.</summary>
    [HttpPost("accept-invitation")]
    public async Task<IActionResult> AcceptInvitation(
        [FromBody] AcceptInvitationRequest request, CancellationToken ct)
    {
        bool ok = await _auth.AcceptInvitationAsync(request, ct);
        return ok
            ? Ok(new MessageResponse { Message = "Password set. Please sign in." })
            : BadRequest(new MessageResponse { Message = "This invitation link is invalid or has expired." });
    }

    private string? Ip() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? UserAgent() => Request.Headers.UserAgent.ToString();
}
