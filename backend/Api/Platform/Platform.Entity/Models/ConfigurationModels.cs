using System.ComponentModel.DataAnnotations;

namespace Platform.Entity.Models;

/// <summary>A config key with its effective value for one organization.</summary>
public class ConfigurationDto
{
    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>Number / Text / Boolean / Date / Json — drives which input the UI renders.</summary>
    public string DataType { get; set; } = null!;

    public string? Category { get; set; }

    /// <summary>The shipped value, shown alongside so a user can see what they changed from.</summary>
    public string DefaultValue { get; set; } = null!;

    public string Value { get; set; } = null!;

    public bool IsOverridden { get; set; }

    /// <summary>Chosen once, then frozen once the branch has traded.</summary>
    public bool IsOneTime { get; set; }

    /// <summary>
    /// True when <see cref="IsOneTime"/> and the freeze has actually bitten.
    /// The screen reads this rather than <see cref="IsOneTime"/>, so a setting
    /// stays editable right up until the first document is posted.
    /// </summary>
    public bool IsLocked { get; set; }
}

public class SetConfigurationRequest
{
    [Required(ErrorMessage = "Value is required.")]
    [MaxLength(1000, ErrorMessage = "Value cannot exceed 1000 characters.")]
    public string Value { get; set; } = null!;
}
