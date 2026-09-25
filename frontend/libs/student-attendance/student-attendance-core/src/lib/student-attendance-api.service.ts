import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { RegisterRow, RegisterView } from './student-attendance.models';

/** The student attendance routes (S3, TK-63), behind the Gateway's `/api/student-attendance`. */
@Injectable({ providedIn: 'root' })
export class StudentAttendanceApiService {
  private readonly http = inject(HttpClient);

  register(sectionId: number, date: string): Promise<RegisterView> {
    return firstValueFrom(
      this.http.get<RegisterView>('/api/student-attendance/register', { params: { sectionId: String(sectionId), date } }),
    );
  }

  save(sectionId: number, attendanceDate: string, rows: RegisterRow[]): Promise<void> {
    return firstValueFrom(this.http.put<void>('/api/student-attendance/register', { sectionId, attendanceDate, rows }));
  }

  lock(sectionId: number, attendanceDate: string): Promise<void> {
    return firstValueFrom(this.http.post<void>('/api/student-attendance/register/lock', { sectionId, attendanceDate }));
  }

  unlock(sectionId: number, attendanceDate: string): Promise<void> {
    return firstValueFrom(this.http.post<void>('/api/student-attendance/register/unlock', { sectionId, attendanceDate }));
  }
}
