using System;
using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

public class AssetTransaction : OrgScopedEntity
{
    public long AssetTransactionId { get; set; }

    public long FixedAssetId { get; set; }

    public AssetTransactionType TransactionType { get; set; }

    public long? DepreciationScheduleId { get; set; }

    public DateOnly TransactionDate { get; set; }

    public decimal Amount { get; set; }
    
    public long? JournalId { get; set; }

    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }
}
