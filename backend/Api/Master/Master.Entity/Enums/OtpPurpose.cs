namespace Master.Entity.Enums;

public enum OtpPurpose
{
    PasswordReset = 1,
    EmailConfirm = 2,
    MobileConfirm = 3,

    /// <summary>
    /// Proves the mobile number on a public trial signup before anything is
    /// created (TK-196). Issued with no user, because none exists yet.
    /// </summary>
    TrialSignup = 4,
}
