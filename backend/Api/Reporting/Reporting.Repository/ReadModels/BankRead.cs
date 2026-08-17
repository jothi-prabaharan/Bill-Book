#pragma warning disable CS8618
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>acc.Banks</c>, read-only. The institution a bank account belongs to.
/// </summary>
public class BankRead : OrgScopedEntity
{
    public long BankId { get; set; }

    public string BankCode { get; set; }
    public string BankName { get; set; }
    public bool IsActive { get; set; }
}


