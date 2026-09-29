using TimeLeave.Repository.Clients;
using Shared.Kernel.Interfaces;

namespace TimeLeave.Repository.Repositories;

public interface ICallerPermissions
{
    bool Has(string permission);
    Guid? UserId { get; }
}
