export type EmploymentType = 'Permanent' | 'Probation' | 'Contract' | 'PartTime' | 'Intern' | 'Consultant';
export type OpeningStatus = 'Draft' | 'Open' | 'OnHold' | 'Closed' | 'Filled';
export type CandidateSource = 'Portal' | 'Referral' | 'Agency' | 'CareersPage' | 'WalkIn';
export type ApplicationStage = 'Applied' | 'Screening' | 'Interview' | 'Offer' | 'Hired' | 'Rejected' | 'Withdrawn';
export type RoundKind = 'Telephonic' | 'Technical' | 'Hr' | 'Managerial';
export type InterviewOutcome = 'Pending' | 'Pass' | 'Fail' | 'NoShow';
export type OfferStatus = 'Draft' | 'Approved' | 'Sent' | 'Accepted' | 'Declined' | 'Revoked';
export type ApprovalStatus = 'Draft' | 'PendingApproval' | 'Approved' | 'Rejected';

export interface JobRequisitionListItem {
  jobRequisitionId: number;
  requisitionCode: string;
  departmentId: number;
  designationId: number;
  gradeId: number;
  workLocationId: number;
  openings: number;
  employmentType: string;
  minCtc: number;
  maxCtc: number;
  justification: string;
  isReplacement: boolean;
  approvalStatus: string;
  currentStepLabel?: string | null;
  activeOpeningsCount: number;
}

export interface CreateJobRequisition {
  departmentId: number;
  designationId: number;
  gradeId: number;
  workLocationId: number;
  openings: number;
  employmentType: EmploymentType;
  minCtc: number;
  maxCtc: number;
  justification: string;
  isReplacement: boolean;
  replacesEmployeeId?: number | null;
}

export interface JobOpeningListItem {
  jobOpeningId: number;
  jobRequisitionId: number;
  requisitionCode: string;
  title: string;
  description: string;
  openingStatus: string;
  publishedDate?: string | null;
  closingDate?: string | null;
  applicationsCount: number;
}

export interface CreateJobOpening {
  jobRequisitionId: number;
  title: string;
  description: string;
  openingStatus: OpeningStatus;
  publishedDate?: string | null;
  closingDate?: string | null;
}

export interface CandidateListItem {
  candidateId: number;
  fullName: string;
  email: string;
  phone: string;
  currentEmployer?: string | null;
  currentCtc?: number | null;
  expectedCtc?: number | null;
  noticePeriodDays?: number | null;
  candidateSource: string;
  resumeAttachmentKey?: string | null;
  applicationsCount: number;
}

export interface CreateCandidate {
  firstName: string;
  lastName?: string | null;
  email: string;
  phone: string;
  currentEmployer?: string | null;
  currentCtc?: number | null;
  expectedCtc?: number | null;
  noticePeriodDays?: number | null;
  candidateSource: CandidateSource;
  referredByEmployeeId?: number | null;
  resumeAttachmentKey?: string | null;
}

export interface ApplicationListItem {
  applicationId: number;
  jobOpeningId: number;
  openingTitle: string;
  candidateId: number;
  candidateName: string;
  candidateEmail: string;
  candidatePhone: string;
  stage: string;
  rejectionReason?: string | null;
  interviewRoundsCount: number;
  hasOffer: boolean;
  offerStatus?: string | null;
}

export interface CreateApplication {
  jobOpeningId: number;
  candidateId: number;
}

export interface UpdateApplicationStage {
  stage: ApplicationStage;
  rejectionReason?: string | null;
}

export interface ScheduleInterviewRound {
  applicationId: number;
  roundNo: number;
  roundKind: RoundKind;
  scheduledAt: string;
  interviewerEmployeeId: number;
}

export interface UpdateInterviewFeedback {
  rating?: number | null;
  feedback?: string | null;
  outcome: InterviewOutcome;
}

export interface InterviewRoundView {
  interviewRoundId: number;
  applicationId: number;
  roundNo: number;
  roundKind: string;
  scheduledAt: string;
  interviewerEmployeeId: number;
  rating?: number | null;
  feedback?: string | null;
  outcome: string;
}

export interface CreateOffer {
  applicationId: number;
  offeredCtc: number;
  salaryStructureId: number;
  joiningDate: string;
}

export interface OfferView {
  offerId: number;
  applicationId: number;
  offeredCtc: number;
  salaryStructureId: number;
  joiningDate: string;
  offerStatus: string;
  approvalStatus: string;
  currentStepLabel?: string | null;
  acceptedAt?: string | null;
  createdEmployeeId?: number | null;
  candidateName?: string | null;
  openingTitle?: string | null;
}

export interface AcceptOfferResult {
  offerId: number;
  employeeId: number;
  employeeCode: string;
  alreadyExisted: boolean;
  message: string;
}
