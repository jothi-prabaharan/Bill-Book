using Performance.Repository.Clients;
using Shared.Kernel.Interfaces;

namespace Performance.Repository.Repositories;

public interface ICallerPermissions
{
    bool Has(string permission);
    Guid? UserId { get; }
}
