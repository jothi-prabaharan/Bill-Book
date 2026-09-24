using System.ComponentModel.DataAnnotations;
using Payroll.Entity.Enums;

namespace Payroll.Entity.Models;

public sealed class CreateFnfSettlementRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Employee ID is required.")]
    public long EmployeeId { get; set; }

    public long? SeparationId { get; set; }

    public DateOnly LastWorkingDate { get; set; }

    public Guid? LinkedUserId { get; set; }

    public int NoticeShortfallDays { get; set; }

    public int CompletedYearsOfService { get; set; }

    [MaxLength(500, ErrorMessage = "Remarks cannot exceed 500 characters.")]
    public string? Remarks { get; set; }
}

public sealed class SaveFnfSettlementRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Employee ID is required.")]
    public long EmployeeId { get; set; }

    public long? SeparationId { get; set; }

    public DateOnly LastWorkingDate { get; set; }

    public Guid? LinkedUserId { get; set; }

    public List<SaveFnfLineRequest> Lines { get; set; } = [];

    [MaxLength(500, ErrorMessage = "Remarks cannot exceed 500 characters.")]
    public string? Remarks { get; set; }
}

public sealed class SaveFnfLineRequest
{
    public FnfLineKind Kind { get; set; }

    public decimal Amount { get; set; }

    public bool IsDeduction { get; set; }

    [MaxLength(200, ErrorMessage = "Remarks cannot exceed 200 characters.")]
    public string? Remarks { get; set; }
}
