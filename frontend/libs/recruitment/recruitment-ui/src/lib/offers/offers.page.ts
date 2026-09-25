import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ApplicationListItem,
  CreateOffer,
  OfferView,
  RecruitmentApiService,
} from '@bill-book/recruitment-core';

@Component({
  selector: 'rec-offers-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './offers.page.html',
  styleUrl: '../recruitment-page.scss',
})
export class OffersPage implements OnInit {
  private readonly api = inject(RecruitmentApiService);

  readonly offers = signal<OfferView[]>([]);
  readonly applications = signal<ApplicationListItem[]>([]);
  readonly loading = signal(false);
  readonly showForm = signal(false);
  readonly successMessage = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);

  readonly form = signal<CreateOffer>({
    applicationId: 0,
    offeredCtc: 650000,
    salaryStructureId: 1,
    joiningDate: new Date(Date.now() + 14 * 86400000).toISOString().slice(0, 10),
  });

  async ngOnInit(): Promise<void> {
    await this.load();
    try {
      const apps = await this.api.applications(1, 100);
      this.applications.set(apps);
      if (apps.length > 0) {
        this.form.update(f => ({ ...f, applicationId: apps[0].applicationId }));
      }
    } catch {
      // ignore
    }
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const data = await this.api.offers();
      this.offers.set(data);
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to load offers');
    } finally {
      this.loading.set(false);
    }
  }

  openCreate(): void {
    const firstAppId = this.applications().length > 0 ? this.applications()[0].applicationId : 0;
    this.form.set({
      applicationId: firstAppId,
      offeredCtc: 650000,
      salaryStructureId: 1,
      joiningDate: new Date(Date.now() + 14 * 86400000).toISOString().slice(0, 10),
    });
    this.showForm.set(true);
  }

  cancelForm(): void {
    this.showForm.set(false);
  }

  async save(): Promise<void> {
    const data = this.form();
    if (!data.applicationId) {
      this.errorMessage.set('Application selection is required.');
      return;
    }
    if (data.offeredCtc <= 0) {
      this.errorMessage.set('Offered CTC must be greater than zero.');
      return;
    }

    this.loading.set(true);
    try {
      await this.api.createOffer(data);
      this.showForm.set(false);
      await this.load();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to save offer');
    } finally {
      this.loading.set(false);
    }
  }

  async approve(id: number): Promise<void> {
    try {
      await this.api.approveOffer(id);
      await this.load();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Approve failed');
    }
  }

  async send(id: number): Promise<void> {
    try {
      await this.api.sendOffer(id);
      await this.load();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Send failed');
    }
  }

  async decline(id: number): Promise<void> {
    try {
      await this.api.declineOffer(id);
      await this.load();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Decline failed');
    }
  }

  async revoke(id: number): Promise<void> {
    try {
      await this.api.revokeOffer(id);
      await this.load();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Revoke failed');
    }
  }

  async accept(id: number): Promise<void> {
    this.loading.set(true);
    this.successMessage.set(null);
    this.errorMessage.set(null);
    try {
      const result = await this.api.acceptOffer(id);
      this.successMessage.set(
        `${result.message} (Employee Code: ${result.employeeCode}, ID: ${result.employeeId})`
      );
      await this.load();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Accept failed');
    } finally {
      this.loading.set(false);
    }
  }
}
