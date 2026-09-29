using Employee.Repository.Clients;
using Shared.Kernel.Interfaces;

namespace Employee.Repository.Repositories;

/// <summary>What the caller may see, for masking (TK-48). From the token.</summary>
public interface ICallerPermissions
{
    bool Has(string permission);

    Guid? UserId { get; }
}
