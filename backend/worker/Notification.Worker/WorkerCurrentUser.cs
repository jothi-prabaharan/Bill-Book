using System;
using Shared.Kernel.Interfaces;

namespace Notification.Worker;

public class WorkerCurrentUser : ICurrentUser
{
    public Guid? UserId => Guid.Empty;
    public Guid? CustomerId => null;
    public Guid? OrgId => null;
    public int? RoleId => null;
}
