using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.Services;
using Platform.Entity.Models;

namespace Platform.Api.Controllers;

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
        SignupResponse response = await _signup.SignupAsync(request, ct);
        return AcceptedAtAction(nameof(GetStatus), new { customerId = response.CustomerId }, response);
    }

    /// <summary>Polled by the signup screen until CanLogin is true.</summary>
    [HttpGet("{customerId:guid}/status")]
    public async Task<IActionResult> GetStatus(Guid customerId, CancellationToken ct)
    {
        CustomerStatusResponse? status = await _signup.GetStatusAsync(customerId, ct);
        return status is null ? NotFound() : Ok(status);
    }
}
