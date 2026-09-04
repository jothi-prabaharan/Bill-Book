namespace Master.Api.Services;

public abstract class AuthException : Exception
{
    protected AuthException(string message) : base(message)
    {
    }
}

public sealed class InvalidCredentialsException : AuthException
{
    public InvalidCredentialsException() : base("Invalid email or password.")
    {
    }
}

public sealed class AccountLockedException : AuthException
{
    public AccountLockedException(DateTimeOffset until)
        : base("Account is locked. Try again later.")
    {
        Until = until;
    }

    public DateTimeOffset Until { get; }
}

public sealed class NoOrganizationAccessException : AuthException
{
    public NoOrganizationAccessException() : base("This account has no organization access.")
    {
    }
}

public sealed class DatabaseNotReadyException : AuthException
{
    public DatabaseNotReadyException() : base("Your account is still being set up. Please try again shortly.")
    {
    }
}

/// <summary>
/// The presented refresh token is not usable — unknown, expired, revoked by a
/// password reset or a logout, or lost a race to another refresh.
///
/// <b>One exception for all of those on purpose.</b> Answering differently for
/// "expired" and "never existed" tells a caller holding a guess which half of
/// the guess was right, and the client's behaviour is the same in every case:
/// sign in again.
/// </summary>
public sealed class InvalidRefreshTokenException : AuthException
{
    public InvalidRefreshTokenException() : base("Your session has ended. Please sign in again.")
    {
    }
}

/// <summary>
/// A refresh token that had already been spent was presented again, so its whole
/// family was revoked.
///
/// Separate from <see cref="InvalidRefreshTokenException"/> for the log and for
/// the security event, never for the response: the API answers both alike, or
/// the difference becomes a way to probe which stolen tokens are still live.
/// </summary>
public sealed class RefreshTokenReuseException : AuthException
{
    public RefreshTokenReuseException()
        : base("Your session has ended. Please sign in again.")
    {
    }
}
