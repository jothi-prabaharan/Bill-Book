namespace Master.Api.Services;

/// <summary>
/// Creates the owner user during provisioning.
///
/// This was the seam from Platform to Identity, back when the two were separate
/// services and provisioning had to reach the idn tables over an internal API.
/// Both are this service now and <see cref="InProcessIdentityAdmin"/> writes the
/// rows directly. The interface stays because provisioning runs on a background
/// worker with no request scope of its own, and naming what it needs is what
/// keeps that worker's dependencies visible.
/// </summary>
public interface IIdentityAdmin
{
    Task CreateOwnerUserAsync(CreateOwnerUser request, CancellationToken ct = default);
}

public sealed record CreateOwnerUser(
    Guid OrgId,
    string Email,
    string DisplayName,
    string? MobileNumber,
    string Password);
