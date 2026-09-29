using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Customer.Entity.TableEntities;

public class EmailTemplate : OrgScopedEntity
{
    public long EmailTemplateId { get; set; }

    [Required(ErrorMessage = "Template name is required.")]
    [MaxLength(100, ErrorMessage = "Template name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Subject is required.")]
    [MaxLength(200, ErrorMessage = "Subject cannot exceed 200 characters.")]
    public string Subject { get; set; } = null!;

    [Required(ErrorMessage = "HTML body is required.")]
    public string HtmlBody { get; set; } = null!;

    public string? TextBody { get; set; }

    public bool IsActive { get; set; } = true;
}
