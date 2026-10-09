/**
 * Models and enums for the Site Services module (TK-168 to TK-170).
 */

export type SiteStatus = 'Planning' | 'Active' | 'OnHold' | 'Completed' | 'Closed';
export type AssignmentStatus = 'Active' | 'Ended';
export type MarginScope = 'Assignment' | 'Site' | 'Trade' | 'Item' | 'Branch';
export type MarginAppliesTo = 'All' | 'Labor' | 'Overtime' | 'Material';
export type MarginType = 'Percent' | 'FlatPerUnit';

export interface TradeDto {
  tradeId: number;
  tradeCode: string;
  tradeName: string;
  defaultServiceItemId?: number | null;
  defaultCostRatePerDay?: number | null;
  defaultSellRatePerDay: number;
  defaultOtCostRatePerHour?: number | null;
  defaultOtSellRatePerHour: number;
  isActive: boolean;
}

export interface SaveTradeDto {
  tradeCode: string;
  tradeName: string;
  defaultServiceItemId?: number | null;
  defaultCostRatePerDay: number;
  defaultSellRatePerDay: number;
  defaultOtCostRatePerHour: number;
  defaultOtSellRatePerHour: number;
  isActive: boolean;
}

export interface LaborerDto {
  laborerId: number;
  tradeId: number;
  userId?: string | null;
  employeeId?: number | null;
  fullName: string;
  phone: string;
  costRatePerDay?: number | null;
  sellRatePerDay?: number | null;
  otCostRatePerHour?: number | null;
  otSellRatePerHour?: number | null;
  isActive: boolean;
}

export interface SaveLaborerDto {
  tradeId: number;
  userId?: string | null;
  employeeId?: number | null;
  fullName: string;
  phone: string;
  costRatePerDay?: number | null;
  sellRatePerDay?: number | null;
  otCostRatePerHour?: number | null;
  otSellRatePerHour?: number | null;
  isActive: boolean;
}

export interface SiteDto {
  siteId: number;
  siteCode: string;
  siteName: string;
  contactId: number;
  address: string;
  latitude?: number | null;
  longitude?: number | null;
  geofenceRadiusMeters: number;
  warehouseId?: number | null;
  status: SiteStatus;
  startDate?: string | null;
  estimatedEndDate?: string | null;
  actualEndDate?: string | null;
}

export interface SaveSiteDto {
  siteCode?: string | null;
  siteName: string;
  contactId: number;
  address: string;
  latitude?: number | null;
  longitude?: number | null;
  geofenceRadiusMeters: number;
  status: SiteStatus;
  startDate?: string | null;
  estimatedEndDate?: string | null;
  actualEndDate?: string | null;
}

export interface SiteAssignmentDto {
  siteAssignmentId: number;
  siteId: number;
  laborerId: number;
  tradeId: number;
  startDate: string;
  endDate?: string | null;
  costRatePerDay?: number | null;
  sellRatePerDay?: number | null;
  approvedDays: number;
  approvedOtHours: number;
  billedDays: number;
  billedOtHours: number;
  status: AssignmentStatus;
}

export interface SaveAssignmentDto {
  siteId: number;
  laborerId: number;
  tradeId: number;
  startDate: string;
  endDate?: string | null;
  costRatePerDay?: number | null;
  sellRatePerDay?: number | null;
  status: AssignmentStatus;
}

export interface MarginRuleDto {
  marginRuleId: number;
  scope: MarginScope;
  scopeId?: number | null;
  appliesTo: MarginAppliesTo;
  marginType: MarginType;
  marginValue?: number | null;
  effectiveFrom: string;
  effectiveTo?: string | null;
  isActive: boolean;
}

export interface SaveMarginRuleDto {
  scope: MarginScope;
  scopeId?: number | null;
  appliesTo: MarginAppliesTo;
  marginType: MarginType;
  marginValue: number;
  effectiveFrom: string;
  effectiveTo?: string | null;
  isActive: boolean;
}

export interface ResolveMarginRequest {
  assignmentId?: number | null;
  siteId?: number | null;
  tradeId?: number | null;
  itemId?: number | null;
  appliesTo: MarginAppliesTo;
  date?: string | null;
  cost: number;
}

export interface ResolveMarginResponse {
  rule?: MarginRuleDto | null;
  marginAmount?: number | null;
  sellingPrice: number;
}

export type AttendanceStatus = 'CheckedIn' | 'Submitted' | 'Approved' | 'Rejected';

export interface SiteAttendanceSummaryDto {
  attendanceId: number;
  workDate: string;
  checkInTime: string;
  clockOutTime?: string | null;
  status: AttendanceStatus;
  isGeofenceBreached: boolean;
  dayFraction: number;
  otHoursClaimed: number;
}

export interface LaborerPortalAssignmentDto {
  assignmentId: number;
  siteId: number;
  siteCode: string;
  siteName: string;
  address: string;
  latitude?: number | null;
  longitude?: number | null;
  radiusMeters: number;
  laborerId: number;
  laborerName: string;
  tradeId: number;
  tradeName: string;
  startDate: string;
  endDate?: string | null;
  todayAttendance?: SiteAttendanceSummaryDto | null;
}

export interface CheckInPortalRequest {
  assignmentId: number;
  latitude?: number | null;
  longitude?: number | null;
}

export interface SiteAttendancePhotoDto {
  photoId: number;
  attendanceId: number;
  fileName: string;
  contentType: string;
  fileSize: number;
  uploadedAt: string;
}

export interface SiteAttendanceDetailDto {
  attendanceId: number;
  assignmentId: number;
  siteId: number;
  siteName: string;
  laborerId: number;
  laborerName: string;
  workDate: string;
  checkInTime: string;
  checkInLat?: number | null;
  checkInLng?: number | null;
  isGeofenceBreached: boolean;
  clockOutTime?: string | null;
  dayFraction: number;
  otHoursClaimed: number;
  otHoursApproved: number;
  workSummary?: string | null;
  status: AttendanceStatus;
  rejectionReason?: string | null;
  photos: SiteAttendancePhotoDto[];
}

