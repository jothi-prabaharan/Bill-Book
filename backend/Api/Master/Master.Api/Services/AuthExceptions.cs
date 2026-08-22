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
