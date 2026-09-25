/** The parent portal's shapes (S9, TK-69), as Student, Fee and Attendance send them. */

export interface PortalChild {
  studentId: number;
  studentName: string;
  admissionNo: string;
  className: string | null;
  sectionName: string | null;
  academicYearCode: string | null;
  isActive: boolean;
}

export interface PortalDemandLine {
  feeHeadName: string;
  amount: number;
  concessionAmount: number;
}

export interface PortalDemand {
  feeDemandId: number;
  demandNo: string;
  studentId: number;
  periodKey: string;
  demandDate: string;
  dueDate: string;
  totalAmount: number;
  concessionAmount: number;
  netAmount: number;
  paidAmount: number;
  balance: number;
  lines: PortalDemandLine[];
}

export interface PortalReceipt {
  feeReceiptId: number;
  receiptNo: string;
  receiptDate: string;
  paymentMode: string;
  amount: number;
  unallocatedAmount: number;
  demandNos: string[];
}

export type AttendanceStatus = 'Present' | 'Absent' | 'Late' | 'HalfDay' | 'Leave' | 'Holiday';

export interface PortalAttendance {
  studentId: number;
  month: string;
  days: { attendanceDate: string; attendanceStatus: AttendanceStatus; remarks: string | null }[];
  present: number;
  absent: number;
  late: number;
  halfDay: number;
  leave: number;
  holiday: number;
}

export interface PortalExam {
  examId: number;
  examName: string;
  academicYearCode: string;
  startDate: string;
  endDate: string;
  subjects: { subjectName: string; maxMarks: number; passMarks: number; marks: number | null; isAbsent: boolean; passed: boolean }[];
}

/** What is still owed across a guardian's demands. */
export const totalDue = (demands: PortalDemand[]): number => demands.reduce((sum, d) => sum + d.balance, 0);

/** Days the child was in school, a half day counting as half, over days the school was open. */
export function attendancePercent(a: Pick<PortalAttendance, 'present' | 'absent' | 'late' | 'halfDay' | 'leave'>): number | null {
  const open = a.present + a.absent + a.late + a.halfDay + a.leave;
  return open === 0 ? null : Math.round(((a.present + a.late + a.halfDay / 2) / open) * 100);
}
