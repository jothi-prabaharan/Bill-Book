using Shared.Kernel.Interfaces;

namespace Master.Api.Services;

/// <summary>Master serves public reference data — there is no caller identity.</summary>
public sealed class AnonymousCurrentUser : ICurrentUser
{
    public Guid? UserId => null;

    public Guid? CustomerId => null;

    public Guid? OrgId => null;

    /// <summary>No role: an anonymous caller holds none.</summary>
    public int? RoleId => null;
}
