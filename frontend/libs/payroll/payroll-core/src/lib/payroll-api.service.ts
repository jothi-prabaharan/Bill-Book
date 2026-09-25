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

  // Statutory
  pfSetting(): Promise<any> {
    return firstValueFrom(this.http.get<any>('/api/payroll/statutory/pf'));
  }

  savePfSetting(body: any): Promise<{ id: number }> {
    return firstValueFrom(this.http.post<{ id: number }>('/api/payroll/statutory/pf', body));
  }

  esiSetting(): Promise<any> {
    return firstValueFrom(this.http.get<any>('/api/payroll/statutory/esi'));
  }

  saveEsiSetting(body: any): Promise<{ id: number }> {
    return firstValueFrom(this.http.post<{ id: number }>('/api/payroll/statutory/esi', body));
  }

  ptSlabs(stateId?: number): Promise<any[]> {
    const params: Record<string, string> = {};
    if (stateId !== undefined && stateId !== null) {
      params['stateId'] = String(stateId);
    }
    return firstValueFrom(this.http.get<any[]>('/api/payroll/statutory/pt-slabs', { params }));
  }

  savePtSlab(body: any): Promise<{ id: number }> {
    return firstValueFrom(this.http.post<{ id: number }>('/api/payroll/statutory/pt-slabs', body));
  }

  // Tax
  taxDeclaration(employeeId: number, financialYear = '2026-2027'): Promise<any> {
    return firstValueFrom(
      this.http.get<any>(`/api/payroll/tax/declarations/${employeeId}`, {
        params: { financialYear },
      }),
    );
  }

  saveTaxDeclaration(body: any): Promise<{ id: number }> {
    return firstValueFrom(this.http.post<{ id: number }>('/api/payroll/tax/declarations', body));
  }

  lockTaxDeclaration(id: number): Promise<void> {
    return firstValueFrom(this.http.post<void>(`/api/payroll/tax/declarations/${id}/lock`, {}));
  }

  unlockTaxDeclaration(id: number): Promise<void> {
    return firstValueFrom(this.http.post<void>(`/api/payroll/tax/declarations/${id}/unlock`, {}));
  }

  previousEmployerIncome(employeeId: number, financialYear = '2026-2027'): Promise<any> {
    return firstValueFrom(
      this.http.get<any>(`/api/payroll/tax/previous-employer/${employeeId}`, {
        params: { financialYear },
      }),
    );
  }

  savePreviousEmployerIncome(body: any): Promise<{ id: number }> {
    return firstValueFrom(this.http.post<{ id: number }>('/api/payroll/tax/previous-employer', body));
  }

  taxSlabs(financialYear = '2026-2027', regime = 'New'): Promise<any[]> {
    return firstValueFrom(
      this.http.get<any[]>('/api/payroll/tax/slabs', {
        params: { financialYear, regime },
      }),
    );
  }

  computeTax(employeeId: number, financialYear: string, annualGross: number, remainingMonths = 12): Promise<any> {
    return firstValueFrom(
      this.http.post<any>('/api/payroll/tax/compute', null, {
        params: {
          employeeId: String(employeeId),
          financialYear,
          annualGross: String(annualGross),
          remainingMonths: String(remainingMonths),
        },
      }),
    );
  }

  // Full & Final
  calculateFnf(body: any): Promise<{ id: number }> {
    return firstValueFrom(this.http.post<{ id: number }>('/api/payroll/fnf/calculate', body));
  }

  fnfSettlement(id: number): Promise<any> {
    return firstValueFrom(this.http.get<any>(`/api/payroll/fnf/${id}`));
  }

  fnfSettlementByEmployee(employeeId: number): Promise<any> {
    return firstValueFrom(this.http.get<any>(`/api/payroll/fnf/employee/${employeeId}`));
  }

  approveFnf(id: number): Promise<void> {
    return firstValueFrom(this.http.post<void>(`/api/payroll/fnf/${id}/approve`, {}));
  }

  postFnf(id: number, linkedUserId?: string): Promise<{ runId: number }> {
    const params: Record<string, string> = {};
    if (linkedUserId) params['linkedUserId'] = linkedUserId;
    return firstValueFrom(this.http.post<{ runId: number }>(`/api/payroll/fnf/${id}/post`, null, { params }));
  }

  // Self-Service (H8, TK-55)
  myPayslips(): Promise<import('./payroll.models').MyPayslipSummary[]> {
    return firstValueFrom(this.http.get<import('./payroll.models').MyPayslipSummary[]>('/api/payroll/me/payslips'));
  }

  myPayslip(id: number): Promise<import('./payroll.models').PayslipView> {
    return firstValueFrom(this.http.get<import('./payroll.models').PayslipView>(`/api/payroll/me/payslips/${id}`));
  }

  downloadPayslip(id: number): Promise<string> {
    return firstValueFrom(this.http.get(`/api/payroll/me/payslips/${id}/download`, { responseType: 'text' }));
  }

  myForm16(): Promise<any[]> {
    return firstValueFrom(this.http.get<any[]>('/api/payroll/me/form16'));
  }

  myTaxDeclaration(financialYear?: string): Promise<any> {
    const params: Record<string, string> = {};
    if (financialYear) params['financialYear'] = financialYear;
    return firstValueFrom(this.http.get<any>('/api/payroll/me/tax-declarations', { params }));
  }

  submitMyTaxDeclaration(body: any): Promise<any> {
    return firstValueFrom(this.http.post<any>('/api/payroll/me/tax-declarations', body));
  }
}
