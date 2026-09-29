using Recruitment.Repository.Clients;
using Shared.Kernel.Interfaces;

namespace Recruitment.Repository.Repositories;

public interface ICallerPermissions
{
    bool Has(string permission);
    Guid? UserId { get; }
}
