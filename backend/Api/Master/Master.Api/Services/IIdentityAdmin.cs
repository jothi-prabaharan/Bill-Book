namespace Master.Api.Services;

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
public interface IIdentityAdmin
{
    Task CreateOwnerUserAsync(CreateOwnerUser request, CancellationToken ct = default);
}

/// <param name="App">The app signed up for; the owner gets that app's Owner role (TK-45).</param>
public sealed record CreateOwnerUser(
    Guid OrgId,
    string Email,
    string DisplayName,
    string? MobileNumber,
    string Password,
    Shared.Kernel.Apps.App App = Shared.Kernel.Apps.App.RetailErp);
