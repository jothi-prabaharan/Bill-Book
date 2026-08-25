namespace Master.Entity.Enums;

/// <summary>
/// Lifecycle status shared by Customer and Organization. `Expired` applies to
/// Customer only. `Failed` applies to Customer signup only — a branch created
/// under an existing, already-active customer instead stays at `Provisioning`
/// forever until retried, because an authenticated owner can always come back
/// to it; a public, unauthenticated signup has no such second visit; it needs
/// a terminal state so the screen can stop polling and show an error.
/// </summary>
public enum TenantStatus
{
    Provisioning = 1,
    Active = 2,
    Suspended = 3,
    Trial = 4,
    Expired = 5,
    Failed = 6,
}
