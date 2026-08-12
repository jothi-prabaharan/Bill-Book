using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

/// <summary>The currencies an organization transacts in — a per-org subset of mst.Currencies.</summary>
public class OrgCurrency : AuditableEntity
{
    public Guid OrgCurrencyId { get; set; }

    public Guid OrgId { get; set; }

    public int CurrencyId { get; set; }

    /// <summary>Exactly one true per org (partial unique index).</summary>
    public bool IsBaseCurrency { get; set; }

    public bool IsActive { get; set; } = true;
}
