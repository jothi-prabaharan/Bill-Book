/** The admission service's shapes (S2, TK-62), as the API sends them: enums by name. */

export type EnquirySource = 'WalkIn' | 'Website' | 'Referral' | 'Advertisement' | 'Other';
export type EnquiryStatus = 'Open' | 'FollowUp' | 'Converted' | 'Lost';
export type ApplicationStage = 'Submitted' | 'DocumentsVerified' | 'Assessed' | 'Offered' | 'Admitted' | 'Rejected' | 'Withdrawn';
export type DocumentKind = 'BirthCertificate' | 'TransferCertificate' | 'ReportCard' | 'Photo' | 'AddressProof' | 'Other';
export type ChildGender = 'Male' | 'Female' | 'Other' | 'NotStated';
export type ParentRelationship = 'Father' | 'Mother' | 'Guardian' | 'Other';

export interface Enquiry {
  enquiryId: number;
  enquiryDate: string;
  childName: string;
  dateOfBirth: string | null;
  seekingClassId: number;
  academicYearId: number;
  parentName: string;
  phone: string;
  email: string | null;
  enquirySource: EnquirySource;
  enquiryStatus: EnquiryStatus;
  followUpDate: string | null;
}

export type SaveEnquiry = Omit<Enquiry, 'enquiryId'>;

export interface ApplicationDocument {
  applicationDocumentId?: number;
  documentKind: DocumentKind;
  attachmentKey: string | null;
  remarks: string | null;
  isVerified: boolean;
}

export interface SaveApplication {
  enquiryId: number | null;
  applicationDate: string;
  childFirstName: string;
  childLastName: string | null;
  dateOfBirth: string;
  childGender: ChildGender;
  seekingClassId: number;
  academicYearId: number;
  guardianName: string;
  guardianPhone: string;
  guardianEmail: string | null;
  guardianRelationship: ParentRelationship;
  applicationFee: number;
  documents: ApplicationDocument[];
}

export interface Application extends SaveApplication {
  applicationId: number;
  applicationNo: string;
  guardianContactId: number | null;
  applicationStage: ApplicationStage;
  assessmentScore: number | null;
  admittedStudentId: number | null;
  admissionNo: string | null;
}

export interface AdmitRequest {
  admissionDate: string;
  sectionId: number | null;
  rollNo: number | null;
}

export interface AdmitResponse {
  studentId: number;
  admissionNo: string;
  guardianContactId: number;
}
