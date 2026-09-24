import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  PayrollRunView,
  PayslipView,
  SalaryComponentView,
  SalaryStructureView,
  SaveSalaryComponent,
  SaveSalaryStructure,
} from './payroll.models';

@Injectable({ providedIn: 'root' })
export class PayrollApiService {
  private readonly http = inject(HttpClient);

  // Components
  components(): Promise<SalaryComponentView[]> {
    return firstValueFrom(this.http.get<SalaryComponentView[]>('/api/payroll/salary-components'));
  }

  saveComponent(id: number | null, body: SaveSalaryComponent): Promise<{ id: number }> {
    return firstValueFrom(
      id === null
        ? this.http.post<{ id: number }>('/api/payroll/salary-components', body)
        : this.http.put<{ id: number }>(`/api/payroll/salary-components/${id}`, body),
    );
  }

  deleteComponent(id: number): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`/api/payroll/salary-components/${id}`));
  }

  // Structures
  structures(): Promise<SalaryStructureView[]> {
    return firstValueFrom(this.http.get<SalaryStructureView[]>('/api/payroll/salary-structures'));
  }

  saveStructure(id: number | null, body: SaveSalaryStructure): Promise<{ id: number }> {
    return firstValueFrom(
      id === null
        ? this.http.post<{ id: number }>('/api/payroll/salary-structures', body)
        : this.http.put<{ id: number }>(`/api/payroll/salary-structures/${id}`, body),
    );
  }

  // Runs
  runs(): Promise<PayrollRunView[]> {
    return firstValueFrom(this.http.get<PayrollRunView[]>('/api/payroll/runs'));
  }

  run(id: number): Promise<PayrollRunView> {
    return firstValueFrom(this.http.get<PayrollRunView>(`/api/payroll/runs/${id}`));
  }

  payslips(runId: number): Promise<PayslipView[]> {
    return firstValueFrom(this.http.get<PayslipView[]>(`/api/payroll/runs/${runId}/payslips`));
  }

  processRun(month: string): Promise<{ id: number }> {
    return firstValueFrom(this.http.post<{ id: number }>('/api/payroll/runs/process', { month }));
  }

  approveRun(id: number): Promise<void> {
    return firstValueFrom(this.http.post<void>(`/api/payroll/runs/${id}/approve`, {}));
  }

  postRun(id: number): Promise<void> {
    return firstValueFrom(this.http.post<void>(`/api/payroll/runs/${id}/post`, {}));
  }

  markPaidRun(id: number): Promise<void> {
    return firstValueFrom(this.http.post<void>(`/api/payroll/runs/${id}/mark-paid`, {}));
  }

  reverseRun(id: number): Promise<void> {
    return firstValueFrom(this.http.post<void>(`/api/payroll/runs/${id}/reverse`, {}));
  }
}
