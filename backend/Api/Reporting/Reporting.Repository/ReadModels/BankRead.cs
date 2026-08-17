using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>acc.Banks</c>, read-only. The institution a bank account belongs to.
/// </summary>
public class BankRead : OrgScopedEntity
{
    public long BankId { get; set; }

    public string BankCode { get; set; } = null!;

    public string BankName { get; set; } = null!;

    public bool IsActive { get; set; }
}
