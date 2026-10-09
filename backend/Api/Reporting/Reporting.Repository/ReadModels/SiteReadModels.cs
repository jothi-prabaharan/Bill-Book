using System;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

// -----------------------------------------------------------------------------
// sit schema read models
// -----------------------------------------------------------------------------

public class SiteJobRead : OrgScopedEntity
{
    public long SiteId { get; set; }
    public string SiteCode { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public long ContactId { get; set; }
    public string Address { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int GeofenceRadiusMeters { get; set; }
    public long? WarehouseId { get; set; }
    public int Status { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EstimatedEndDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }
}

public class SiteTradeRead : OrgScopedEntity
{
    public long TradeId { get; set; }
    public string TradeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long? DefaultServiceItemId { get; set; }
    public decimal DefaultDayCost { get; set; }
    public decimal DefaultOtCost { get; set; }
    public bool IsActive { get; set; }
}

public class SiteLaborerRead : OrgScopedEntity
{
    public long LaborerId { get; set; }
    public string LaborerCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public long TradeId { get; set; }
    public Guid? UserId { get; set; }
    public long? EmployeeId { get; set; }
    public bool IsActive { get; set; }
}

public class SiteAssignmentRead : OrgScopedEntity
{
    public long AssignmentId { get; set; }
    public long SiteId { get; set; }
    public long LaborerId { get; set; }
    public long TradeId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int Status { get; set; }
    public decimal BilledDays { get; set; }
    public decimal BilledOtHours { get; set; }
    public decimal ApprovedDays { get; set; }
    public decimal ApprovedOtHours { get; set; }
}

public class SiteAttendanceRead : OrgScopedEntity
{
    public long AttendanceId { get; set; }
    public long AssignmentId { get; set; }
    public long SiteId { get; set; }
    public long LaborerId { get; set; }
    public DateOnly WorkDate { get; set; }
    public DateTime CheckInTime { get; set; }
    public decimal? CheckInLat { get; set; }
    public decimal? CheckInLng { get; set; }
    public bool IsGeofenceBreached { get; set; }
    public DateTime? ClockOutTime { get; set; }
    public decimal DayFraction { get; set; }
    public decimal OtHoursClaimed { get; set; }
    public decimal OtHoursApproved { get; set; }
    public string? WorkSummary { get; set; }
    public int Status { get; set; }
    public long? ApprovalRequestId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public long? ApprovedBy { get; set; }
    public string? RejectionReason { get; set; }
    public decimal DayCostSnapshot { get; set; }
    public decimal DayMarginSnapshot { get; set; }
    public decimal DaySellSnapshot { get; set; }
    public decimal OtCostSnapshot { get; set; }
    public decimal OtMarginSnapshot { get; set; }
    public decimal OtSellSnapshot { get; set; }
    public long? SiteBillLineId { get; set; }
}

public class SiteStockBalanceRead : OrgScopedEntity
{
    public long BalanceId { get; set; }
    public long SiteId { get; set; }
    public long ItemId { get; set; }
    public decimal DispatchedQty { get; set; }
    public decimal ReturnedQty { get; set; }
    public decimal UsedQty { get; set; }
    public decimal OnSiteQty { get; set; }
    public decimal UnbilledUsedQty { get; set; }
    public decimal BilledQty { get; set; }
}

public class SiteBillingInvoiceRead : OrgScopedEntity
{
    public long SiteBillingInvoiceId { get; set; }
    public string BillingNumber { get; set; } = string.Empty;
    public long SiteId { get; set; }
    public DateOnly BillingDate { get; set; }
    public long? SalesInvoiceId { get; set; }
    public decimal TotalLaborAmount { get; set; }
    public decimal TotalMaterialAmount { get; set; }
    public decimal TotalBillableAmount { get; set; }
    public bool IsFinalBilling { get; set; }
}

public class SiteBillingInvoiceLineRead : OrgScopedEntity
{
    public long SiteBillingInvoiceLineId { get; set; }
    public long SiteBillingInvoiceId { get; set; }
    public long? AttendanceId { get; set; }
    public long? UsageLineId { get; set; }
    public string ItemDescription { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal TotalAmount { get; set; }
}

public class SiteStockDocumentRead : OrgScopedEntity
{
    public long DocumentId { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public DateOnly DocumentDate { get; set; }
    public long SiteId { get; set; }
    public int DocumentType { get; set; }
    public long? RelatedTransferId { get; set; }
    public long? ApprovalRequestId { get; set; }
    public int Status { get; set; }
}

public class SiteStockLineRead : OrgScopedEntity
{
    public long LineId { get; set; }
    public long DocumentId { get; set; }
    public long ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal? MarginValue { get; set; }
    public decimal? SellingPrice { get; set; }
    public decimal BilledQuantity { get; set; }
}
