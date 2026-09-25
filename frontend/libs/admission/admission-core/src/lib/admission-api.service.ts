import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  AdmitRequest,
  AdmitResponse,
  Application,
  ApplicationStage,
  Enquiry,
  EnquiryStatus,
  SaveApplication,
  SaveEnquiry,
} from './admission.models';

type Saved = { id: number };

/** The Admission service's routes (S2, TK-62), behind the Gateway's `/api/admission`. */
@Injectable({ providedIn: 'root' })
export class AdmissionApiService {
  private readonly http = inject(HttpClient);

  enquiries(status?: EnquiryStatus | null): Promise<Enquiry[]> {
    return firstValueFrom(this.http.get<Enquiry[]>('/api/admission/enquiries', { params: status ? { status } : {} }));
  }

  saveEnquiry(id: number | null, body: SaveEnquiry): Promise<Saved> {
    return firstValueFrom(
      id === null
        ? this.http.post<Saved>('/api/admission/enquiries', body)
        : this.http.put<Saved>(`/api/admission/enquiries/${id}`, body),
    );
  }

  applications(stage?: ApplicationStage | null): Promise<Application[]> {
    return firstValueFrom(this.http.get<Application[]>('/api/admission/applications', { params: stage ? { stage } : {} }));
  }

  application(id: number): Promise<Application> {
    return firstValueFrom(this.http.get<Application>(`/api/admission/applications/${id}`));
  }

  saveApplication(id: number | null, body: SaveApplication): Promise<Saved> {
    return firstValueFrom(
      id === null
        ? this.http.post<Saved>('/api/admission/applications', body)
        : this.http.put<Saved>(`/api/admission/applications/${id}`, body),
    );
  }

  move(id: number, applicationStage: ApplicationStage, assessmentScore: number | null): Promise<Saved> {
    return firstValueFrom(this.http.post<Saved>(`/api/admission/applications/${id}/stage`, { applicationStage, assessmentScore }));
  }

  admit(id: number, body: AdmitRequest): Promise<AdmitResponse> {
    return firstValueFrom(this.http.post<AdmitResponse>(`/api/admission/applications/${id}/admit`, body));
  }
}
