using System.ComponentModel.DataAnnotations;
using Shared.Kernel.CustomFields;
using Shared.Kernel.Tenancy;

namespace Master.Entity.TableEntities;

/// <summary>
/// One custom field a branch has defined for one kind of record (TK-110,
/// design "Custom fields and custom reports"), in the tenant schema
/// <c>cfd</c>. The values live on the records themselves, in a jsonb column
/// keyed by <see cref="Key"/>; every owning service validates them against
/// these definitions. The key is permanent. A field is deactivated, never
/// deleted, so no stored value is orphaned.
/// </summary>
public class CustomFieldDefinition : OrgScopedEntity
{
    public long CustomFieldDefinitionId { get; set; }

    public CustomFieldEntityKind EntityKind { get; set; }

    [Required(ErrorMessage = "Key is required.")]
    [MaxLength(40, ErrorMessage = "Key cannot exceed 40 characters.")]
    [RegularExpression("^[a-z][a-z0-9_]*$", ErrorMessage = "A key starts with a lowercase letter and holds only lowercase letters, digits and underscores.")]
    public string Key { get; set; } = null!;

    [Required(ErrorMessage = "Label is required.")]
    [MaxLength(80, ErrorMessage = "Label cannot exceed 80 characters.")]
    public string Label { get; set; } = null!;

    public CustomFieldDataType DataType { get; set; }

    /// <summary>For a dropdown or multi-select field. Stored as jsonb.</summary>
    public List<CustomFieldOption> Options { get; set; } = [];

    public bool IsRequired { get; set; }

    [MaxLength(4000, ErrorMessage = "Default value cannot exceed 4000 characters.")]
    public string? DefaultValue { get; set; }

    [Range(1, 4000, ErrorMessage = "Maximum length must be between 1 and 4000.")]
    public int? MaxLength { get; set; }

    public bool ShowInList { get; set; }

    public bool ShowOnPrint { get; set; }

    public bool IsSearchable { get; set; }

    public bool CarryForward { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>One option of a dropdown or multi-select field, held in the definition's jsonb column.</summary>
public class CustomFieldOption
{
    [Required(ErrorMessage = "Option value is required.")]
    [MaxLength(60, ErrorMessage = "Option value cannot exceed 60 characters.")]
    public string Value { get; set; } = null!;

    [Required(ErrorMessage = "Option label is required.")]
    [MaxLength(80, ErrorMessage = "Option label cannot exceed 80 characters.")]
    public string Label { get; set; } = null!;

    public bool Active { get; set; } = true;
}
