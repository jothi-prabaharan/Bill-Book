import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  LaborerPortalAssignmentDto,
  SiteApiService,
  SiteAttendanceSummaryDto,
} from '@bill-book/site-core';

@Component({
  selector: 'bb-laborer-portal-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './laborer-portal.page.html',
  styleUrl: './laborer-portal.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LaborerPortalPage implements OnInit {
  private readonly siteApi = inject(SiteApiService);

  readonly loading = signal<boolean>(false);
  readonly checkingIn = signal<boolean>(false);
  readonly submittingClockOut = signal<boolean>(false);
  readonly assignments = signal<LaborerPortalAssignmentDto[]>([]);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  // Active clock-out form state
  readonly activeClockOut = signal<{
    assignment: LaborerPortalAssignmentDto;
    attendance: SiteAttendanceSummaryDto;
  } | null>(null);

  readonly dayFraction = signal<number>(1.0);
  readonly otHours = signal<number>(0);
  readonly workSummary = signal<string>('');
  readonly selectedFiles = signal<File[]>([]);
  readonly photoPreviews = signal<string[]>([]);

  ngOnInit(): void {
    void this.loadAssignments();
  }

  async loadAssignments(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const data = await this.siteApi.getPortalAssignments();
      this.assignments.set(data);
    } catch (err: unknown) {
      const msg = (err as { error?: { message?: string } })?.error?.message ?? 'Failed to load assignments.';
      this.errorMessage.set(msg);
    } finally {
      this.loading.set(false);
    }
  }

  async checkIn(assignment: LaborerPortalAssignmentDto): Promise<void> {
    this.checkingIn.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    let lat: number | undefined;
    let lng: number | undefined;

    if (navigator.geolocation) {
      try {
        const pos = await new Promise<GeolocationPosition>((resolve, reject) => {
          navigator.geolocation.getCurrentPosition(resolve, reject, {
            enableHighAccuracy: true,
            timeout: 10000,
            maximumAge: 0,
          });
        });
        lat = pos.coords.latitude;
        lng = pos.coords.longitude;
      } catch (err) {
        console.warn('Geolocation unavailable or denied:', err);
      }
    }

    try {
      const att = await this.siteApi.checkIn({
        assignmentId: assignment.assignmentId,
        latitude: lat,
        longitude: lng,
      });

      if (att.isGeofenceBreached) {
        this.successMessage.set('Checked in successfully (flagged outside geofence boundary).');
      } else {
        this.successMessage.set('Checked in successfully within site perimeter!');
      }

      await this.loadAssignments();
    } catch (err: unknown) {
      const msg = (err as { error?: { message?: string } })?.error?.message ?? 'Check-in failed.';
      this.errorMessage.set(msg);
    } finally {
      this.checkingIn.set(false);
    }
  }

  openClockOutForm(assignment: LaborerPortalAssignmentDto): void {
    if (!assignment.todayAttendance) return;
    this.activeClockOut.set({
      assignment,
      attendance: assignment.todayAttendance,
    });
    this.dayFraction.set(1.0);
    this.otHours.set(0);
    this.workSummary.set('');
    this.selectedFiles.set([]);
    this.photoPreviews.set([]);
    this.errorMessage.set(null);
    this.successMessage.set(null);
  }

  cancelClockOut(): void {
    this.activeClockOut.set(null);
  }

  onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) return;

    const files = Array.from(input.files);
    this.selectedFiles.update((current) => [...current, ...files]);

    for (const file of files) {
      const url = URL.createObjectURL(file);
      this.photoPreviews.update((previews) => [...previews, url]);
    }
  }

  removeFile(index: number): void {
    this.selectedFiles.update((files) => files.filter((_, i) => i !== index));
    this.photoPreviews.update((previews) => {
      const removed = previews[index];
      if (removed) URL.revokeObjectURL(removed);
      return previews.filter((_, i) => i !== index);
    });
  }

  async submitClockOut(): Promise<void> {
    const active = this.activeClockOut();
    if (!active) return;

    const files = this.selectedFiles();
    if (files.length === 0) {
      this.errorMessage.set('At least one work-proof photo is required.');
      return;
    }

    const summary = this.workSummary().trim();
    if (!summary) {
      this.errorMessage.set('Work summary is required.');
      return;
    }

    this.submittingClockOut.set(true);
    this.errorMessage.set(null);

    try {
      const formData = new FormData();
      formData.append('AttendanceId', active.attendance.attendanceId.toString());
      formData.append('DayFraction', this.dayFraction().toString());
      formData.append('OtHoursClaimed', this.otHours().toString());
      formData.append('WorkSummary', summary);

      for (const file of files) {
        formData.append('photos', file, file.name);
      }

      await this.siteApi.clockOut(formData);
      this.successMessage.set('Clock-out submitted successfully with photo proof!');
      this.activeClockOut.set(null);
      await this.loadAssignments();
    } catch (err: unknown) {
      const msg = (err as { error?: { message?: string } })?.error?.message ?? 'Clock-out submission failed.';
      this.errorMessage.set(msg);
    } finally {
      this.submittingClockOut.set(false);
    }
  }
}
