/** The Student service's shapes (S1, TK-61), as the API sends them: enums by name. */

export type SubjectKind = 'Core' | 'Language' | 'Elective' | 'CoCurricular';
export type Gender = 'Male' | 'Female' | 'Other' | 'NotStated';
export type StudentStatus = 'Active' | 'Alumni' | 'Withdrawn' | 'Transferred';
export type GuardianRelationship = 'Father' | 'Mother' | 'Guardian' | 'Other';
export type EnrolmentStatus = 'Active' | 'Promoted' | 'Detained' | 'Withdrawn';
export type ExamStatus = 'Planned' | 'MarksOpen' | 'Published' | 'Locked';

export interface AcademicYear {
  academicYearId: number;
  code: string;
  startDate: string;
  endDate: string;
  isCurrent: boolean;
  isClosed: boolean;
}

export interface SchoolClass {
  schoolClassId: number;
  code: string;
  name: string;
  sortOrder: number;
  isActive: boolean;
}

export interface Section {
  sectionId: number;
  academicYearId: number;
  academicYearCode: string;
  schoolClassId: number;
  className: string;
  name: string;
  capacity: number | null;
  enrolled: number;
  classTeacherEmployeeId: number | null;
  roomSpaceId: number | null;
}

export interface Subject {
  subjectId: number;
  code: string;
  name: string;
  subjectKind: SubjectKind;
  isActive: boolean;
}

export interface StudentGuardian {
  contactId: number;
  relationship: GuardianRelationship;
  isPrimary: boolean;
  hasPortalAccess: boolean;
  displayName?: string | null;
}

export interface EnrolRequest {
  studentId?: number;
  academicYearId: number;
  sectionId: number;
  rollNo: number | null;
}

export interface SaveStudent {
  firstName: string;
  lastName: string | null;
  dateOfBirth: string;
  gender: Gender;
  admissionDate: string;
  studentStatus: StudentStatus;
  leavingDate: string | null;
  bloodGroup: string | null;
  nationalId: string | null;
  guardians: StudentGuardian[];
  enrol?: EnrolRequest | null;
}

export interface StudentListItem {
  studentId: number;
  admissionNo: string;
  fullName: string;
  gender: Gender;
  studentStatus: StudentStatus;
  className: string | null;
  sectionName: string | null;
  rollNo: number | null;
  nationalId: string | null;
}

export interface EnrolmentView {
  enrolmentId: number;
  academicYearId: number;
  academicYearCode: string;
  sectionId: number;
  className: string;
  sectionName: string;
  rollNo: number | null;
  enrolmentStatus: EnrolmentStatus;
}

export interface StudentView extends SaveStudent {
  studentId: number;
  admissionNo: string;
  sourceApplicationId: number | null;
  enrolments: EnrolmentView[];
}

export interface ExamSubject {
  examSubjectId?: number;
  subjectId: number;
  schoolClassId: number;
  maxMarks: number;
  passMarks: number;
}

export interface Exam {
  examId: number;
  academicYearId: number;
  name: string;
  startDate: string;
  endDate: string;
  examStatus: ExamStatus;
  subjects: ExamSubject[];
}

export interface SaveExam {
  academicYearId: number;
  name: string;
  startDate: string;
  endDate: string;
  subjects: ExamSubject[];
}

export interface MarkRow {
  enrolmentId: number;
  rollNo: number | null;
  studentName: string | null;
  marks: number | null;
  isAbsent: boolean;
}

export interface RollEntry {
  enrolmentId: number;
  studentId: number;
  admissionNo: string;
  fullName: string;
  rollNo: number | null;
}

/** A guardian contact, as the contacts list returns it, for the guardian picker. */
export interface GuardianContact {
  contactId: number;
  contactCode: string;
  displayName: string;
}
