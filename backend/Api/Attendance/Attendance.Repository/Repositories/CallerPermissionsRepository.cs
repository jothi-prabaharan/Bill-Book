

namespace Attendance.Repository.Repositories;

/// <summary>What the caller may do beyond the route's own permission (S3, TK-63). From the token.</summary>
public interface ICallerPermissions
{
    bool Has(string permission);
}
