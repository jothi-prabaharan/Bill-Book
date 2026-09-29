namespace Shared.Kernel.Internal;

/// <summary>
/// <c>GET internal/users/branch</c>: one active user of a branch, with the
/// permissions a token for that branch would carry now (TK-114). Read at the
/// moment of use by work that runs later on someone's behalf, a scheduled
/// report say, so a permission taken away yesterday is not used today.
/// Only users holding an active role in the branch are listed.
/// </summary>
public sealed class BranchUserAccess
{
    public Guid UserId { get; set; }

    public string DisplayName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public List<string> Permissions { get; set; } = [];
}
