using Master.Repository.Clients;

namespace Master.Repository.Repositories;

/// <summary>
/// Creates the owner user during provisioning.
///
/// This was the seam from Platform to Identity, back when the two were separate
/// services and provisioning had to reach the mst tables over an internal API.
/// Both are this service now and <see cref="InProcessIdentityAdmin"/> writes the
/// rows directly. The interface stays because provisioning runs on a background
/// worker with no request scope of its own, and naming what it needs is what
/// keeps that worker's dependencies visible.
/// </summary>
public interface IIdentityAdminRepository
{
    Task CreateOwnerUserAsync(CreateOwnerUser request, CancellationToken ct = default);
}

/// <param name="IsMobileConfirmed">
/// True when the signup proved the number with a WhatsApp or SMS code (TK-196).
/// Only a new user takes it; an existing account keeps its own.
/// </param>
/// <param name="IsEmailConfirmed">
/// True when the signup proved the address with a code sent to it (TK-197),
/// on the same terms.
/// </param>
public sealed record CreateOwnerUser(
    Guid OrgId,
    string Email,
    string DisplayName,
    string? MobileNumber,
    string Password,
    bool IsMobileConfirmed = false,
    bool IsEmailConfirmed = false);
