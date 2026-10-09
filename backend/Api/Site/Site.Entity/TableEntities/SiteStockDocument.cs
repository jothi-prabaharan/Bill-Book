using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Shared.Kernel.Tenancy;
using Site.Entity.Enums;

namespace Site.Entity.TableEntities;

/// <summary>
/// Document header for Dispatch (SDP), Return (SRT), and Usage (SUS) (TK-173, TK-174).
/// </summary>
[Table("SiteStockDocuments", Schema = "sit")]
public class SiteStockDocument : OrgScopedEntity
{
    [Key]
    public long DocumentId { get; set; }

    [Required(ErrorMessage = "Document type is required.")]
    public SiteStockDocumentType DocumentType { get; set; }

    [Required(ErrorMessage = "Document number is required.")]
    [StringLength(30, ErrorMessage = "Document number cannot exceed 30 characters.")]
    public string DocumentNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Site is required.")]
    public long SiteId { get; set; }

    /// <summary>Central warehouse for dispatch or destination for return.</summary>
    public long? SourceWarehouseId { get; set; }

    [Required(ErrorMessage = "Document date is required.")]
    public DateOnly DocumentDate { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    public SiteStockDocumentStatus Status { get; set; } = SiteStockDocumentStatus.Draft;

    public long? ApprovalRequestId { get; set; }

    [StringLength(500, ErrorMessage = "Remarks cannot exceed 500 characters.")]
    public string? Remarks { get; set; }
}
