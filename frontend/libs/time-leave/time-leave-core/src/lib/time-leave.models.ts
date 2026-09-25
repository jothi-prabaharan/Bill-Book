export interface LeaveTypeView {
  leaveTypeId: number;
  code: string;
  name: string;
  isPaid: boolean;
  isHalfDayAllowed: boolean;
  isAttachmentRequiredAboveDays?: number | null;
  gender?: string | null;
  isActive: boolean;
}

export interface LeavePolicyView {
  leavePolicyId: number;
  leaveTypeId: number;
  leaveTypeName?: string | null;
  gradeId?: number | null;
  workLocationId?: number | null;
  effectiveFrom: string;
  annualQuota: number;
  accrualKind: string;
  isProratedOnJoining: boolean;
  carryForwardKind: string;
  maxCarryForward?: number | null;
  maxEncashPerYear?: number | null;
  minDaysPerApplication?: number | null;
  maxDaysPerApplication?: number | null;
  noticeDays: number;
  isSandwichRule: boolean;
  canApplyInProbation: boolean;
}

export interface LeaveBalanceView {
  leaveBalanceId: number;
  employeeId: number;
  leaveTypeId: number;
  leaveTypeCode: string;
  leaveTypeName: string;
  leaveYear: number;
  opening: number;
  accrued: number;
  taken: number;
  encashed: number;
  lapsed: number;
  adjusted: number;
  available: number;
}

export interface ApplyLeave {
  employeeId: number;
  leaveTypeId: number;
  fromDate: string;
  toDate: string;
  fromHalf: 'Full' | 'FirstHalf' | 'SecondHalf';
  toHalf: 'Full' | 'FirstHalf' | 'SecondHalf';
  reason: string;
  attachmentKey?: string | null;
}

export interface LeaveApplicationView {
  leaveApplicationId: number;
  employeeId: number;
  leaveTypeId: number;
  leaveTypeCode: string;
  leaveTypeName: string;
  fromDate: string;
  toDate: string;
  fromHalf: string;
  toHalf: string;
  days: number;
  reason: string;
  attachmentKey?: string | null;
  leaveStatus: string;
  approvalStatus: string;
  currentStepLabel?: string | null;
  currentApproverEmployeeId?: number | null;
}

export interface LeaveEncashmentView {
  leaveEncashmentId: number;
  employeeId: number;
  leaveTypeId: number;
  leaveTypeCode: string;
  leaveTypeName: string;
  leaveYear: number;
  days: number;
  encashmentStatus: string;
  approvalStatus: string;
  currentStepLabel?: string | null;
}

export interface ShiftView {
  shiftId: number;
  code: string;
  name: string;
  startTime: string;
  endTime: string;
  breakMinutes: number;
  graceInMinutes: number;
  graceOutMinutes: number;
  halfDayBelowMinutes: number;
  absentBelowMinutes: number;
  isNightShift: boolean;
  isActive: boolean;
}

export interface WeeklyOffPolicyView {
  weeklyOffPolicyId: number;
  name: string;
  mondayRule: string;
  tuesdayRule: string;
  wednesdayRule: string;
  thursdayRule: string;
  fridayRule: string;
  saturdayRule: string;
  sundayRule: string;
  alternateWeeks?: string | null;
  isActive: boolean;
}

export interface DailyAttendanceView {
  dailyAttendanceId: number;
  employeeId: number;
  attendanceDate: string;
  shiftId?: number | null;
  shiftName?: string | null;
  firstIn?: string | null;
  lastOut?: string | null;
  workedMinutes: number;
  lateMinutes: number;
  earlyOutMinutes: number;
  overtimeMinutes: number;
  attendanceStatus: string;
  attendanceSource: string;
  isLocked: boolean;
}

export interface RegularisationRequestView {
  regularisationRequestId: number;
  employeeId: number;
  attendanceDate: string;
  requestedIn?: string | null;
  requestedOut?: string | null;
  requestedStatus: string;
  reason: string;
  approvalStatus: string;
  currentStepLabel?: string | null;
}
