namespace Master.Entity.Enums;

/// <summary>
/// How a one-time code reaches the person.
///
/// <b>Email is the default wherever a channel is chosen</b> (TK-197): every
/// signup and every account has an address, and it goes through the platform's
/// own mailbox, so it works wherever mail is configured. WhatsApp and SMS are
/// the other choices on both trial signup and password reset, to the mobile
/// number (TK-196).
/// </summary>
public enum OtpChannel
{
    Email = 1,
    Sms = 2,
    WhatsApp = 3,
}
