using System.ComponentModel.DataAnnotations;

namespace Inventory.Entity.Models;

public class UomTypeListItem
{
    public long UomTypeId { get; set; }

    public string? UomTypeSystemName { get; set; }

    public string UomTypeName { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; }

    /// <summary>The units of this type, edited inline beneath it on the page.</summary>
    public List<UnitOfMeasureListItem> Units { get; set; } = [];
}

public class UnitOfMeasureListItem
{
    public long UomId { get; set; }

    public long UomTypeId { get; set; }

    public string? UomSystemName { get; set; }

    public string UomCode { get; set; } = null!;

    public string UqcCode { get; set; } = null!;

    public string UomName { get; set; } = null!;

    public bool IsBaseUnit { get; set; }

    public decimal ConversionToBase { get; set; }

    public int DecimalPlaces { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; }

    /// <summary>True once an item references this unit — after which its type is frozen.</summary>
    public bool IsUsed { get; set; }
}

public class SaveUomTypeRequest
{
    [Required(ErrorMessage = "Unit type name is required.")]
    [MaxLength(50, ErrorMessage = "Unit type name cannot exceed 50 characters.")]
    public string UomTypeName { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}

public class SaveUnitOfMeasureRequest
{
    public long UomTypeId { get; set; }

    [Required(ErrorMessage = "Unit code is required.")]
    [MaxLength(10, ErrorMessage = "Unit code cannot exceed 10 characters.")]
    public string UomCode { get; set; } = null!;

    [Required(ErrorMessage = "UQC code is required.")]
    [MaxLength(3, ErrorMessage = "UQC code must be 3 characters.")]
    public string UqcCode { get; set; } = "OTH";

    [Required(ErrorMessage = "Unit name is required.")]
    [MaxLength(50, ErrorMessage = "Unit name cannot exceed 50 characters.")]
    public string UomName { get; set; } = null!;

    [Range(0.000001, 999999999999.0, ErrorMessage = "Conversion to base must be greater than zero.")]
    public decimal ConversionToBase { get; set; } = 1m;

    [Range(0, 6, ErrorMessage = "Decimal places must be between 0 and 6.")]
    public int DecimalPlaces { get; set; }

    public bool IsActive { get; set; } = true;

    // IsBaseUnit is not here. Moving the base rescales every factor in the type,
    // so it is its own action rather than a checkbox on a save.
}

public enum SaveUomOutcome
{
    Ok = 0,
    NotFound = 1,
    DuplicateName = 2,
    DuplicateCode = 3,
    TypeInUse = 4,
    UnitInUse = 5,
    SystemRowUndeletable = 6,
    /// <summary>The type has units, so it must keep exactly one base unit.</summary>
    BaseUnitRequired = 7,
    /// <summary>The base unit's own factor is fixed at 1.</summary>
    BaseUnitFactorFixed = 8,
    /// <summary>Rescaling the type would restate quantities already recorded.</summary>
    BaseChangeBlocked = 9,
    InvalidValue = 10,
}
