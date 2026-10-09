import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  SiteApiService,
  SiteAttendanceDetailDto,
  SiteDto,
} from '@bill-book/site-core';

@Component({
  selector: 'bb-clockout-approvals-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './clockout-approvals.page.html',
  styleUrl: './clockout-approvals.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClockoutApprovalsPage implements OnInit {
  private readonly siteApi = inject(SiteApiService);

  readonly loading = signal<boolean>(false);
  readonly pendingList = signal<SiteAttendanceDetailDto[]>([]);
  readonly sites = signal<SiteDto[]>([]);
  readonly selectedSiteId = signal<number | null>(null);

  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  // Approval Modal state
  readonly approvingItem = signal<SiteAttendanceDetailDto | null>(null);
  readonly approvedOtHours = signal<number>(0);
  readonly processingAction = signal<boolean>(false);

  // Rejection Modal state
  readonly rejectingItem = signal<SiteAttendanceDetailDto | null>(null);
  readonly rejectionReason = signal<string>('');

  // Photo viewer modal
  readonly viewingPhotoUrl = signal<string | null>(null);

  ngOnInit(): void {
    void this.loadSites();
    void this.loadPending();
  }

  async loadSites(): Promise<void> {
    try {
      const data = await this.siteApi.listSites();
      this.sites.set(data);
    } catch {
      // Ignore
    }
  }

  async loadPending(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const data = await this.siteApi.listPendingAttendance(this.selectedSiteId());
      this.pendingList.set(data);
    } catch (err: unknown) {
      const msg = (err as { error?: { message?: string } })?.error?.message ?? 'Failed to load pending attendances.';
      this.errorMessage.set(msg);
    } finally {
      this.loading.set(false);
    }
  }

  onSiteFilterChange(siteId: number | null): void {
    this.selectedSiteId.set(siteId);
    void this.loadPending();
  }

  openApproveModal(item: SiteAttendanceDetailDto): void {
    this.approvingItem.set(item);
    this.approvedOtHours.set(item.otHoursClaimed);
    this.errorMessage.set(null);
  }

  closeApproveModal(): void {
    this.approvingItem.set(null);
  }

  async confirmApprove(): Promise<void> {
    const item = this.approvingItem();
    if (!item) return;

    if (this.approvedOtHours() < 0 || this.approvedOtHours() > item.otHoursClaimed) {
      this.errorMessage.set(`Approved OT hours must be between 0 and ${item.otHoursClaimed}.`);
      return;
    }

    this.processingAction.set(true);
    this.errorMessage.set(null);
    try {
      await this.siteApi.approveAttendance(item.attendanceId, this.approvedOtHours());
      this.successMessage.set(`Approved attendance for ${item.laborerName} on ${item.siteName}.`);
      this.approvingItem.set(null);
      await this.loadPending();
    } catch (err: unknown) {
      const msg = (err as { error?: { message?: string } })?.error?.message ?? 'Approval failed.';
      this.errorMessage.set(msg);
    } finally {
      this.processingAction.set(false);
    }
  }

  openRejectModal(item: SiteAttendanceDetailDto): void {
    this.rejectingItem.set(item);
    this.rejectionReason.set('');
    this.errorMessage.set(null);
  }

  closeRejectModal(): void {
    this.rejectingItem.set(null);
  }

  async confirmReject(): Promise<void> {
    const item = this.rejectingItem();
    if (!item) return;

    const reason = this.rejectionReason().trim();
    if (!reason) {
      this.errorMessage.set('Rejection reason is required.');
      return;
    }

    this.processingAction.set(true);
    this.errorMessage.set(null);
    try {
      await this.siteApi.rejectAttendance(item.attendanceId, reason);
      this.successMessage.set(`Rejected attendance for ${item.laborerName}.`);
      this.rejectingItem.set(null);
      await this.loadPending();
    } catch (err: unknown) {
      const msg = (err as { error?: { message?: string } })?.error?.message ?? 'Rejection failed.';
      this.errorMessage.set(msg);
    } finally {
      this.processingAction.set(false);
    }
  }

  openPhoto(photoId: number): void {
    this.viewingPhotoUrl.set(this.siteApi.getPhotoUrl(photoId));
  }

  closePhoto(): void {
    this.viewingPhotoUrl.set(null);
  }

  getPhotoUrl(photoId: number): string {
    return this.siteApi.getPhotoUrl(photoId);
  }
}
