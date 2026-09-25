namespace Attendance.Api.Services;

/// <summary>What the caller may do beyond the route's own permission (S3, TK-63). From the token.</summary>
public interface ICallerPermissions
{
    bool Has(string permission);
}

public sealed class HttpCallerPermissions : ICallerPermissions
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCallerPermissions(IHttpContextAccessor accessor) => _accessor = accessor;

    public bool Has(string permission) =>
        _accessor.HttpContext?.User.FindAll("permission")
            .Any(c => string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase)) == true;
}
