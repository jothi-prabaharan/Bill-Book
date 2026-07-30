namespace Platform.Entity.Enums;

/// <summary>
/// The purchased tier. Independent of <see cref="LicenseStatus"/> — an Elite
/// licence can be expired, and a Trial can be in force, so tier and state are
/// deliberately separate enums.
/// </summary>
public enum LicenseType
{
    /// <summary>14 days, 3 users, 1 organization. Created automatically at signup.</summary>
    Trial = 1,

    Standard = 2,

    Pro = 3,

    Elite = 4,
}
