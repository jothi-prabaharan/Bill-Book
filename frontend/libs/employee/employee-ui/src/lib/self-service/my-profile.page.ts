import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { readApiFailure } from '@bill-book/api-client';
import {
  Announcement,
  EmployeeDocument,
  EmployeeApiService,
  MaritalStatus,
  MyProfile,
  UpdateMyProfile,
} from '@bill-book/employee-core';
import {
  BbSelectOption,
  MessageBoxComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
} from '@bill-book/ui-components';

type ProfileTab = 'overview' | 'contact' | 'documents' | 'announcements';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-my-profile-page',
  standalone: true,
  imports: [
    FormsModule,
    TextInputComponent,
    SelectComponent,
    MessageBoxComponent,
  ],
  templateUrl: './my-profile.page.html',
  styleUrl: '../employee-page.scss',
})
export class MyProfilePage implements OnInit {
  private readonly api = inject(EmployeeApiService);

  protected readonly activeTab = signal<ProfileTab>('overview');
  protected readonly profile = signal<MyProfile | null>(null);
  protected readonly announcements = signal<Announcement[]>([]);
  protected readonly documents = signal<EmployeeDocument[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected readonly saving = signal(false);

  protected editForm: UpdateMyProfile = {};

  protected readonly maritalStatusOptions: BbSelectOption<MaritalStatus>[] = [
    { value: 'Single', label: 'Single' },
    { value: 'Married', label: 'Married' },
    { value: 'Widowed', label: 'Widowed' },
    { value: 'Divorced', label: 'Divorced' },
  ];

  ngOnInit(): void {
    void this.loadProfile();
    void this.loadAnnouncements();
    void this.loadDocuments();
  }

  protected setTab(tab: ProfileTab): void {
    this.activeTab.set(tab);
    this.messages.set([]);
  }

  private async loadProfile(): Promise<void> {
    this.busy.set(true);
    try {
      const p = await this.api.myProfile();
      this.profile.set(p);
      this.editForm = {
        phone: p.phone,
        personalEmail: p.personalEmail,
        bloodGroup: p.bloodGroup,
        maritalStatus: (p.maritalStatus as MaritalStatus) || 'Single',
        emergencyContactName: p.emergencyContactName,
        emergencyContactPhone: p.emergencyContactPhone,
      };
    } catch (err) {
      const failure = readApiFailure(err);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.busy.set(false);
    }
  }

  private async loadAnnouncements(): Promise<void> {
    try {
      const list = await this.api.myAnnouncements();
      this.announcements.set(list);
    } catch {
      // Best-effort
    }
  }

  private async loadDocuments(): Promise<void> {
    try {
      const docs = await this.api.myDocuments();
      this.documents.set(docs);
    } catch {
      // Best-effort
    }
  }

  protected async saveContactInfo(): Promise<void> {
    this.saving.set(true);
    this.messages.set([]);
    try {
      const res = await this.api.updateMyProfile(this.editForm);
      this.messages.set([{ tone: 'success', text: res.message || 'Profile updated successfully.' }]);
      await this.loadProfile();
    } catch (err) {
      const failure = readApiFailure(err);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.saving.set(false);
    }
  }
}
