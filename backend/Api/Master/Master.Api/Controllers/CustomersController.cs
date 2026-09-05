using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Master.Api.Services;
using Master.Entity.Models;

namespace Master.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly SignupService _signup;

    public CustomersController(SignupService signup) => _signup = signup;

    /// <summary>Public trial signup. Returns 202 — provisioning is asynchronous.</summary>
    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] SignupRequest request, CancellationToken ct)
    {
        if (await _signup.EmailExistsAsync(request.Email, ct))
        {
            return BadRequest(new { message = "An account with this email already exists." });
        }

        try
        {
            SignupResponse response = await _signup.SignupAsync(request, ct);

            return AcceptedAtAction(
                nameof(GetStatus), new { customerId = response.CustomerId }, response);
        }
        catch (NoTenantCapacityException)
        {
            // 503, not 500: the request was correct and the platform is out of
            // provisioned database capacity. Retrying once an operator has added
            // a shard will work, and saying so is what stops this reading as a
            // bug in the form the person just filled in.
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Signups are temporarily unavailable while capacity is added. "
                    + "Please try again shortly.",
            });
        }
    }

    /// <summary>Polled by the signup screen until CanLogin is true.</summary>
    [HttpGet("{customerId:guid}/status")]
    public async Task<IActionResult> GetStatus(Guid customerId, CancellationToken ct)
    {
        CustomerStatusResponse? status = await _signup.GetStatusAsync(customerId, ct);
        return status is null ? NotFound() : Ok(status);
    }
}
