namespace Master.Entity.Enums;

/// <summary>
/// The effective state of a customer's licence, evaluated per request. Carried
/// in the access token as the `license_status` claim, which the shell gates on.
/// Expiry blocks the app, never the login.
/// </summary>
public enum LicenseStatus
{
    /// <summary>Paid licence, in force.</summary>
    Active = 1,

    /// <summary>Trial licence, still inside its window.</summary>
    Trial = 2,

    /// <summary>Past ExpiryDate + GraceDays. Login succeeds; every feature is refused.</summary>
    Expired = 3,

    /// <summary>Deactivated by an operator, independent of dates.</summary>
    Suspended = 4,

    /// <summary>
    /// The customer holds no licence for the app being signed in to (TK-43).
    /// Treated like Expired: the sign-in works, the app does not.
    /// </summary>
    NotLicensed = 5,
}
