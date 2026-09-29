

namespace Fee.Repository.Repositories;

/// <summary>What the caller may do beyond the route's own permission (S4, TK-64). From the token.</summary>
public interface ICallerPermissions
{
    bool Has(string permission);
}
