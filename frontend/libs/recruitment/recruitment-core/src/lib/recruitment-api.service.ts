import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  AcceptOfferResult,
  ApplicationListItem,
  CandidateListItem,
  CreateApplication,
  CreateCandidate,
  CreateJobOpening,
  CreateJobRequisition,
  CreateOffer,
  InterviewRoundView,
  JobOpeningListItem,
  JobRequisitionListItem,
  OfferView,
  OpeningStatus,
  ScheduleInterviewRound,
  UpdateApplicationStage,
  UpdateInterviewFeedback,
} from './recruitment.models';

@Injectable({ providedIn: 'root' })
export class RecruitmentApiService {
  private readonly http = inject(HttpClient);

  // ---- Requisitions ----
  requisitions(page = 1, pageSize = 20): Promise<JobRequisitionListItem[]> {
    return firstValueFrom(this.http.get<JobRequisitionListItem[]>(`/api/rec/requisitions?page=${page}&pageSize=${pageSize}`));
  }

  getRequisition(id: number): Promise<JobRequisitionListItem> {
    return firstValueFrom(this.http.get<JobRequisitionListItem>(`/api/rec/requisitions/${id}`));
  }

  createRequisition(body: CreateJobRequisition): Promise<JobRequisitionListItem> {
    return firstValueFrom(this.http.post<JobRequisitionListItem>('/api/rec/requisitions', body));
  }

  updateRequisition(id: number, body: CreateJobRequisition): Promise<JobRequisitionListItem> {
    return firstValueFrom(this.http.put<JobRequisitionListItem>(`/api/rec/requisitions/${id}`, body));
  }

  submitRequisition(id: number): Promise<{ success: boolean }> {
    return firstValueFrom(this.http.post<{ success: boolean }>(`/api/rec/requisitions/${id}/submit`, {}));
  }

  approveRequisition(id: number): Promise<{ success: boolean }> {
    return firstValueFrom(this.http.post<{ success: boolean }>(`/api/rec/requisitions/${id}/approve`, {}));
  }

  rejectRequisition(id: number): Promise<{ success: boolean }> {
    return firstValueFrom(this.http.post<{ success: boolean }>(`/api/rec/requisitions/${id}/reject`, {}));
  }

  // ---- Openings ----
  openings(page = 1, pageSize = 20, status?: OpeningStatus): Promise<JobOpeningListItem[]> {
    const qs = status ? `&status=${status}` : '';
    return firstValueFrom(this.http.get<JobOpeningListItem[]>(`/api/rec/openings?page=${page}&pageSize=${pageSize}${qs}`));
  }

  getOpening(id: number): Promise<JobOpeningListItem> {
    return firstValueFrom(this.http.get<JobOpeningListItem>(`/api/rec/openings/${id}`));
  }

  createOpening(body: CreateJobOpening): Promise<JobOpeningListItem> {
    return firstValueFrom(this.http.post<JobOpeningListItem>('/api/rec/openings', body));
  }

  updateOpening(id: number, body: CreateJobOpening): Promise<JobOpeningListItem> {
    return firstValueFrom(this.http.put<JobOpeningListItem>(`/api/rec/openings/${id}`, body));
  }

  // ---- Candidates ----
  candidates(page = 1, pageSize = 20, search?: string): Promise<CandidateListItem[]> {
    const qs = search ? `&search=${encodeURIComponent(search)}` : '';
    return firstValueFrom(this.http.get<CandidateListItem[]>(`/api/rec/candidates?page=${page}&pageSize=${pageSize}${qs}`));
  }

  getCandidate(id: number): Promise<CandidateListItem> {
    return firstValueFrom(this.http.get<CandidateListItem>(`/api/rec/candidates/${id}`));
  }

  createCandidate(body: CreateCandidate): Promise<CandidateListItem> {
    return firstValueFrom(this.http.post<CandidateListItem>('/api/rec/candidates', body));
  }

  updateCandidate(id: number, body: CreateCandidate): Promise<CandidateListItem> {
    return firstValueFrom(this.http.put<CandidateListItem>(`/api/rec/candidates/${id}`, body));
  }

  // ---- Applications & Pipeline ----
  applications(page = 1, pageSize = 50, openingId?: number, stage?: string): Promise<ApplicationListItem[]> {
    const params = new URLSearchParams();
    params.set('page', page.toString());
    params.set('pageSize', pageSize.toString());
    if (openingId) params.set('openingId', openingId.toString());
    if (stage) params.set('stage', stage);
    return firstValueFrom(this.http.get<ApplicationListItem[]>(`/api/rec/applications?${params.toString()}`));
  }

  createApplication(body: CreateApplication): Promise<ApplicationListItem> {
    return firstValueFrom(this.http.post<ApplicationListItem>('/api/rec/applications', body));
  }

  updateApplicationStage(id: number, body: UpdateApplicationStage): Promise<{ success: boolean }> {
    return firstValueFrom(this.http.put<{ success: boolean }>(`/api/rec/applications/${id}/stage`, body));
  }

  // ---- Interviews ----
  interviews(applicationId: number): Promise<InterviewRoundView[]> {
    return firstValueFrom(this.http.get<InterviewRoundView[]>(`/api/rec/interviews/application/${applicationId}`));
  }

  scheduleInterview(body: ScheduleInterviewRound): Promise<InterviewRoundView> {
    return firstValueFrom(this.http.post<InterviewRoundView>('/api/rec/interviews/schedule', body));
  }

  submitInterviewFeedback(id: number, body: UpdateInterviewFeedback): Promise<{ success: boolean }> {
    return firstValueFrom(this.http.put<{ success: boolean }>(`/api/rec/interviews/${id}/feedback`, body));
  }

  // ---- Offers ----
  offers(applicationId?: number): Promise<OfferView[]> {
    const qs = applicationId ? `?applicationId=${applicationId}` : '';
    return firstValueFrom(this.http.get<OfferView[]>(`/api/rec/offers${qs}`));
  }

  getOffer(id: number): Promise<OfferView> {
    return firstValueFrom(this.http.get<OfferView>(`/api/rec/offers/${id}`));
  }

  createOffer(body: CreateOffer): Promise<OfferView> {
    return firstValueFrom(this.http.post<OfferView>('/api/rec/offers', body));
  }

  approveOffer(id: number): Promise<{ success: boolean }> {
    return firstValueFrom(this.http.post<{ success: boolean }>(`/api/rec/offers/${id}/approve`, {}));
  }

  sendOffer(id: number): Promise<{ success: boolean }> {
    return firstValueFrom(this.http.post<{ success: boolean }>(`/api/rec/offers/${id}/send`, {}));
  }

  declineOffer(id: number): Promise<{ success: boolean }> {
    return firstValueFrom(this.http.post<{ success: boolean }>(`/api/rec/offers/${id}/decline`, {}));
  }

  revokeOffer(id: number): Promise<{ success: boolean }> {
    return firstValueFrom(this.http.post<{ success: boolean }>(`/api/rec/offers/${id}/revoke`, {}));
  }

  acceptOffer(id: number): Promise<AcceptOfferResult> {
    return firstValueFrom(this.http.post<AcceptOfferResult>(`/api/rec/offers/${id}/accept`, {}));
  }
}
