import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  AcademicYear,
  EnrolRequest,
  Exam,
  ExamStatus,
  GuardianContact,
  MarkRow,
  RollEntry,
  SaveExam,
  SaveStudent,
  SchoolClass,
  Section,
  StudentListItem,
  StudentStatus,
  StudentView,
  Subject,
} from './sis.models';

type Saved = { id: number };

/** The Sis service's routes (S1, TK-61), behind the Gateway's `/api/sis`. */
@Injectable({ providedIn: 'root' })
export class SisApiService {
  private readonly http = inject(HttpClient);

  years(): Promise<AcademicYear[]> {
    return firstValueFrom(this.http.get<AcademicYear[]>('/api/sis/years'));
  }

  saveYear(id: number | null, body: Omit<AcademicYear, 'academicYearId'>): Promise<Saved> {
    return this.save('/api/sis/years', id, body);
  }

  classes(): Promise<SchoolClass[]> {
    return firstValueFrom(this.http.get<SchoolClass[]>('/api/sis/classes'));
  }

  saveClass(id: number | null, body: Omit<SchoolClass, 'schoolClassId'>): Promise<Saved> {
    return this.save('/api/sis/classes', id, body);
  }

  sections(academicYearId?: number | null): Promise<Section[]> {
    const params: Record<string, string> = academicYearId ? { academicYearId: String(academicYearId) } : {};
    return firstValueFrom(this.http.get<Section[]>('/api/sis/sections', { params }));
  }

  saveSection(id: number | null, body: Partial<Section>): Promise<Saved> {
    return this.save('/api/sis/sections', id, body);
  }

  subjects(): Promise<Subject[]> {
    return firstValueFrom(this.http.get<Subject[]>('/api/sis/subjects'));
  }

  saveSubject(id: number | null, body: Omit<Subject, 'subjectId'>): Promise<Saved> {
    return this.save('/api/sis/subjects', id, body);
  }

  students(query: { search?: string; status?: StudentStatus | null; sectionId?: number | null }): Promise<StudentListItem[]> {
    const params: Record<string, string> = {};
    if (query.search) params['search'] = query.search;
    if (query.status) params['status'] = query.status;
    if (query.sectionId) params['sectionId'] = String(query.sectionId);
    return firstValueFrom(this.http.get<StudentListItem[]>('/api/sis/students', { params }));
  }

  student(id: number): Promise<StudentView> {
    return firstValueFrom(this.http.get<StudentView>(`/api/sis/students/${id}`));
  }

  saveStudent(id: number | null, body: SaveStudent): Promise<Saved> {
    return this.save('/api/sis/students', id, body);
  }

  enrol(body: EnrolRequest): Promise<Saved> {
    return firstValueFrom(this.http.post<Saved>('/api/sis/enrolments', body));
  }

  roll(sectionId: number): Promise<RollEntry[]> {
    return firstValueFrom(this.http.get<RollEntry[]>(`/api/sis/sections/${sectionId}/roll`));
  }

  exams(academicYearId?: number | null): Promise<Exam[]> {
    const params: Record<string, string> = academicYearId ? { academicYearId: String(academicYearId) } : {};
    return firstValueFrom(this.http.get<Exam[]>('/api/sis/exams', { params }));
  }

  saveExam(id: number | null, body: SaveExam): Promise<Saved> {
    return this.save('/api/sis/exams', id, body);
  }

  moveExam(id: number, examStatus: ExamStatus): Promise<Saved> {
    return firstValueFrom(this.http.post<Saved>(`/api/sis/exams/${id}/status`, { examStatus }));
  }

  marks(examId: number, examSubjectId: number, sectionId: number): Promise<MarkRow[]> {
    return firstValueFrom(
      this.http.get<MarkRow[]>(`/api/sis/exams/${examId}/marks`, {
        params: { examSubjectId: String(examSubjectId), sectionId: String(sectionId) },
      }),
    );
  }

  saveMarks(examId: number, examSubjectId: number, marks: MarkRow[]): Promise<Saved> {
    return firstValueFrom(this.http.put<Saved>(`/api/sis/exams/${examId}/marks`, { examSubjectId, marks }));
  }

  /** Guardian contacts, for the guardian picker: Master's contact list, filtered to guardians. */
  guardians(): Promise<GuardianContact[]> {
    return firstValueFrom(this.http.get<GuardianContact[]>('/api/contacts', { params: { role: 'guardian' } }));
  }

  private save<T>(url: string, id: number | null, body: T): Promise<Saved> {
    return firstValueFrom(id === null ? this.http.post<Saved>(url, body) : this.http.put<Saved>(`${url}/${id}`, body));
  }
}
