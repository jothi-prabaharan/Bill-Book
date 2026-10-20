namespace Master.Entity.Enums;

/// <summary>
/// How a one-time code reaches the person.
///
/// <b>WhatsApp is the default wherever a channel is chosen</b> (TK-196): it is
/// what an Indian small-business owner reads first, and SMS is the fallback for
/// a number with no WhatsApp. Email stays for a password reset only, because an
/// account can exist with no mobile number on it; a trial signup is verified by
/// WhatsApp or SMS alone, since proving the mobile number is the point.
/// </summary>
public enum OtpChannel
{
    Email = 1,
    Sms = 2,
    WhatsApp = 3,
}
