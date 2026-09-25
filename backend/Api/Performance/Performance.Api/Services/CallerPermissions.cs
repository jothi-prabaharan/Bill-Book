using Shared.Kernel.Interfaces;

namespace Performance.Api.Services;

public interface ICallerPermissions
{
    bool Has(string permission);
    Guid? UserId { get; }
}

public sealed class HttpCallerPermissions : ICallerPermissions
{
    private readonly IHttpContextAccessor _accessor;
    private readonly ICurrentUser _user;

    public HttpCallerPermissions(IHttpContextAccessor accessor, ICurrentUser user)
    {
        _accessor = accessor;
        _user = user;
    }

    public bool Has(string permission) =>
        _accessor.HttpContext?.User.FindAll("permission")
            .Any(c => string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase)) == true;

    public Guid? UserId => _user.UserId;
}
