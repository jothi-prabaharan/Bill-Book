using Attendance.Repository.Repositories;
namespace Attendance.Api.Services;



public sealed class HttpCallerPermissions : ICallerPermissions
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCallerPermissions(IHttpContextAccessor accessor) => _accessor = accessor;

    public bool Has(string permission) =>
        _accessor.HttpContext?.User.FindAll("permission")
            .Any(c => string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase)) == true;
}
