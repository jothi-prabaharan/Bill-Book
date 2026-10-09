using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Site.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Site.Entity.TableEntities;

/// <summary>
/// Customer job sites / locations (TK-170).
/// </summary>
[Table("Sites", Schema = "sit")]
public class SiteJob : OrgScopedEntity
{
    [Key]
    public long SiteId { get; set; }

    [Required(ErrorMessage = "Site code is required.")]
    [StringLength(30, ErrorMessage = "Site code cannot exceed 30 characters.")]
    public string SiteCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Site name is required.")]
    [StringLength(200, ErrorMessage = "Site name cannot exceed 200 characters.")]
    public string SiteName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Contact is required.")]
    public long ContactId { get; set; }

    [StringLength(500, ErrorMessage = "Address cannot exceed 500 characters.")]
    public string Address { get; set; } = string.Empty;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    [Range(10, 50000, ErrorMessage = "Geofence radius must be between 10 and 50,000 meters.")]
    public int GeofenceRadiusMeters { get; set; } = 200;

    public long? WarehouseId { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    public SiteStatus Status { get; set; } = SiteStatus.Planning;

    public DateOnly? StartDate { get; set; }

    public DateOnly? EstimatedEndDate { get; set; }

    public DateOnly? ActualEndDate { get; set; }
}
