import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { readApiFailure } from '@bill-book/api-client';
import { IfCanDirective } from '@bill-book/auth';
import { Announcement, AnnouncementAudience, HrmApiService } from '@bill-book/hrm-core';
import {
  BbSelectOption,
  CheckboxComponent,
  DateInputComponent,
  MessageBoxComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
  TextareaComponent,
  UiMessage,
} from '@bill-book/ui-components';

/** People › Announcements (H1, TK-48). HRMS only. Pinned ones first, newest next. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-hrm-announcements-page',
  standalone: true,
  imports: [FormsModule, TextInputComponent, TextareaComponent, DateInputComponent, SelectComponent, NumberInputComponent, CheckboxComponent, MessageBoxComponent, IfCanDirective],
  templateUrl: './announcements.page.html',
  styleUrl: '../hrm-page.scss',
})
export class AnnouncementsPage implements OnInit {
  private readonly api = inject(HrmApiService);

  protected readonly rows = signal<Announcement[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected readonly editingId = signal<number | null | undefined>(undefined);
  protected form: Announcement = this.blank();

  protected readonly audiences: BbSelectOption<AnnouncementAudience>[] = [
    { value: 'Everyone', label: 'Everyone' },
    { value: 'Department', label: 'One department' },
    { value: 'Location', label: 'One location' },
    { value: 'Grade', label: 'One grade' },
  ];

  ngOnInit(): void {
    void this.load();
  }

  protected startAdd(): void {
    this.form = this.blank();
    this.editingId.set(null);
  }

  protected startEdit(row: Announcement): void {
    this.form = { ...row };
    this.editingId.set(row.announcementId ?? null);
  }

  protected async save(): Promise<void> {
    if (!this.form.title.trim() || !this.form.body.trim()) {
      this.messages.set([{ tone: 'error', text: 'Give a title and the announcement itself.' }]);
      return;
    }
    this.busy.set(true);
    try {
      await this.api.saveAnnouncement(this.editingId() ?? null, this.form);
      this.messages.set([{ tone: 'success', text: 'The announcement is saved.' }]);
      this.editingId.set(undefined);
      await this.load();
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    try {
      this.rows.set(await this.api.announcements());
    } catch (error) {
      this.fail(error);
    }
  }

  private blank(): Announcement {
    return { title: '', body: '', publishDate: new Date().toISOString().slice(0, 10), audience: 'Everyone', isPinned: false };
  }

  private fail(error: unknown): void {
    const failure = readApiFailure(error);
    this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
  }
}
