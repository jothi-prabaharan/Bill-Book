import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { employeesIn, percentText } from './salary-increment-report.models';
import { SalaryIncrementReportService } from './salary-increment-report.service';

/** The Salary Increment Report's calls and the two helpers its page uses (TK-198). */

function setup(response: unknown = {}) {
  const request = vi.fn().mockReturnValue(of(response));

  TestBed.configureTestingModule({
    providers: [SalaryIncrementReportService, { provide: HttpClient, useValue: { request } }],
  });

  return { service: TestBed.inject(SalaryIncrementReportService), request };
}

describe('SalaryIncrementReportService', () => {
  it('SIR-01: runs on its own endpoint with the filters as the body', async () => {
    const { service, request } = setup({ rows: [] });
    const filters = { fromDate: '2026-04-01', toDate: '2027-03-31', employeeId: 7, departmentId: null };

    await service.run(filters);

    expect(request).toHaveBeenCalledWith('POST', '/api/reports/salary-increment', { body: filters });
  });

  it('SIR-02: reads the filter choices from the options endpoint', async () => {
    const { service, request } = setup({ departments: [], employees: [] });

    await service.options();

    expect(request).toHaveBeenCalledWith('GET', '/api/reports/salary-increment/options', { body: undefined });
  });
});

describe('percentText', () => {
  it('SIR-03: two places, a minus for a cut, and a dash when there is no percentage', () => {
    expect(percentText(10)).toBe('10.00%');
    expect(percentText(-2.5)).toBe('-2.50%');
    expect(percentText(null)).toBe('—');
    expect(percentText(undefined)).toBe('—');
  });
});

describe('employeesIn', () => {
  const employees = [
    { employeeId: 1, employeeCode: 'E001', name: 'Suresh', departmentId: 20 },
    { employeeId: 2, employeeCode: 'E002', name: 'Kavitha R', departmentId: 10 },
  ];

  it('SIR-04: every employee until a department is chosen, then that department only', () => {
    expect(employeesIn(employees, null).map((e) => e.employeeId)).toEqual([1, 2]);
    expect(employeesIn(employees, 10).map((e) => e.employeeId)).toEqual([2]);
  });
});
