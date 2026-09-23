using Reporting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>acc.AssetTransactions</c>, read-only. Everything that has happened to an
/// asset since it was registered.
///
/// <b>This, not the ledger, is the register's own history.</b> A
/// <c>Depreciation</c> row is one month's charge against the asset's schedule;
/// a <c>Disposal</c> row carries the sale proceeds in <c>Amount</c>. Neither
/// capitalisation nor disposal posts to the ledger yet (D-19, D-20), which is
/// exactly what the reconciliation report is there to show.
/// </summary>
public class AssetTransactionRead : OrgScopedEntity
{
    public long AssetTransactionId { get; set; }

    public long FixedAssetId { get; set; }

    public AssetTransactionType TransactionType { get; set; }

    public long? DepreciationScheduleId { get; set; }

    public DateOnly TransactionDate { get; set; }

    public decimal Amount { get; set; }

    public long? JournalId { get; set; }

    public string? Notes { get; set; }
}
