using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Restaurant;

namespace Sales.Entity.Models;

public sealed class SaveFloorRequest
{
    [Required(ErrorMessage = "Enter the floor's name.")]
    [MaxLength(50, ErrorMessage = "A floor's name can be at most 50 characters.")]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public sealed class SaveRestaurantTableRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose the table's floor.")]
    public long FloorId { get; set; }

    [Required(ErrorMessage = "Enter the table's number or code.")]
    [MaxLength(20, ErrorMessage = "A table code can be at most 20 characters.")]
    public string Code { get; set; } = string.Empty;

    [Range(1, 100, ErrorMessage = "A table seats between 1 and 100.")]
    public int Seats { get; set; } = 4;

    public bool IsActive { get; set; } = true;
}

public sealed class SetTableStatusRequest
{
    public TableStatus Status { get; set; }
}

/// <summary>Guests moving from one table to another; their open order goes with them.</summary>
public sealed class MoveTableRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose the table to move to.")]
    public long ToTableId { get; set; }
}

/// <summary>Tables pushed together under a head table, ordered and billed as one.</summary>
public sealed class MergeTablesRequest
{
    [MinLength(1, ErrorMessage = "Choose at least one table to join.")]
    public List<long> TableIds { get; set; } = [];
}

public sealed class FloorPlanView
{
    public List<FloorView> Floors { get; set; } = [];
}

public sealed class FloorView
{
    public long FloorId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    public List<RestaurantTableView> Tables { get; set; } = [];
}

public sealed class RestaurantTableView
{
    public long RestaurantTableId { get; set; }

    public long FloorId { get; set; }

    public string Code { get; set; } = string.Empty;

    public int Seats { get; set; }

    public TableStatus Status { get; set; }

    public long? MergedIntoTableId { get; set; }

    public string? MergedIntoCode { get; set; }

    /// <summary>Codes of the tables merged into this one, when it heads a group.</summary>
    public List<string> MergedCodes { get; set; } = [];

    public bool IsActive { get; set; }

    /// <summary>The open order at this table (or its group's head), once orders exist (TK-146).</summary>
    public long? OpenOrderId { get; set; }
}

/// <summary>How a table or floor write ended (TK-145).</summary>
public enum TableOutcome
{
    Ok = 0,
    NotFound = 1,
    Refused = 2,
}

public sealed record TableResult<T>(TableOutcome Outcome, T? Value = default, string? Message = null)
{
    public static TableResult<T> Done(T value) => new(TableOutcome.Ok, value);

    public static TableResult<T> Missing() => new(TableOutcome.NotFound);

    public static TableResult<T> Refuse(string message) => new(TableOutcome.Refused, default, message);
}
