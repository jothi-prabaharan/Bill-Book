import { ChangeDetectionStrategy, Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ReportQuery,
  ReportRecipient,
  ReportSchedule,
  ReportScheduleFrequency,
  ReportScheduleService,
  WeekDay,
  describeSchedule,
} from '@bill-book/reporting-core';
import { BbSelectOption, CheckboxComponent, SelectComponent, TextInputComponent } from '@bill-book/ui-components';

/**
 * Email this report as an Excel workbook on a schedule (TK-114): the layout on
 * screen, daily, weekly or monthly, to chosen users of the branch. Lists the
 * caller's existing schedules for the report with what each last did.
 *
 * Each run uses the owner's permissions as they are then, so a schedule whose
 * owner loses access sends nothing and says so under **Last run**.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-report-schedule-dialog',
  standalone: true,
  imports: [FormsModule, TextInputComponent, CheckboxComponent, SelectComponent],
  templateUrl: './report-schedule.dialog.html',
  styleUrl: './report-schedule.dialog.scss',
})
export class ReportScheduleDialog implements OnInit {
  private readonly schedules = inject(ReportScheduleService);

  readonly reportKey = input.required<string>();
  readonly reportTitle = input.required<string>();
  readonly layout = input.required<ReportQuery>();
  readonly customReportId = input<number | null>(null);

  readonly closed = output<void>();

  protected readonly existing = signal<ReportSchedule[]>([]);
  protected readonly recipients = signal<ReportRecipient[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);

  protected readonly describe = describeSchedule;

  protected readonly frequencies: BbSelectOption<ReportScheduleFrequency>[] = [
    { value: 'Daily', label: 'Daily' },
    { value: 'Weekly', label: 'Weekly' },
    { value: 'Monthly', label: 'Monthly' },
  ];

  protected readonly weekDays: BbSelectOption<WeekDay>[] = (
    ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'] as WeekDay[]
  ).map((d) => ({ value: d, label: d }));

  protected readonly monthDays: BbSelectOption<number>[] = Array.from({ length: 28 }, (_, i) => ({
    value: i + 1,
    label: String(i + 1),
  }));

  protected readonly hours: BbSelectOption<number>[] = Array.from({ length: 24 }, (_, h) => ({
    value: h,
    label: `${String(h).padStart(2, '0')}:00`,
  }));

  protected title = '';
  protected frequency: ReportScheduleFrequency = 'Weekly';
  protected dayOfWeek: WeekDay = 'Monday';
  protected dayOfMonth = 1;
  protected sendAtHour = 8;
  protected chosen = new Set<string>();

  ngOnInit(): void {
    this.title = `${this.reportTitle()} (scheduled)`;
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    this.message.set(null);

    try {
      const [existing, recipients] = await Promise.all([
        this.schedules.list(this.reportKey()),
        this.schedules.recipients(),
      ]);
      this.existing.set(existing);
      this.recipients.set(recipients);
    } catch (error: unknown) {
      this.message.set(this.serverMessage(error) ?? 'Could not load the schedules.');
    } finally {
      this.busy.set(false);
    }
  }

  protected toggle(userId: string, on: boolean): void {
    const next = new Set(this.chosen);
    if (on) {
      next.add(userId);
    } else {
      next.delete(userId);
    }
    this.chosen = next;
  }

  async save(): Promise<void> {
    if (this.title.trim() === '' || this.chosen.size === 0) {
      return;
    }

    this.busy.set(true);
    this.message.set(null);

    try {
      await this.schedules.create({
        reportKey: this.reportKey(),
        customReportId: this.customReportId(),
        title: this.title.trim(),
        frequency: this.frequency,
        dayOfWeek: this.frequency === 'Weekly' ? this.dayOfWeek : null,
        dayOfMonth: this.frequency === 'Monthly' ? this.dayOfMonth : null,
        sendAtHour: this.sendAtHour,
        timeZoneId: null,
        recipientUserIds: [...this.chosen],
        isActive: true,
        layout: this.layout(),
      });

      this.chosen = new Set();
      await this.load();
    } catch (error: unknown) {
      this.message.set(this.serverMessage(error) ?? 'Could not save this schedule.');
      this.busy.set(false);
    }
  }

  protected async setActive(schedule: ReportSchedule, isActive: boolean): Promise<void> {
    this.busy.set(true);

    try {
      await this.schedules.update(schedule.reportScheduleId, { ...schedule, isActive });
      await this.load();
    } catch (error: unknown) {
      this.message.set(this.serverMessage(error) ?? 'Could not change that schedule.');
      this.busy.set(false);
    }
  }

  protected async remove(schedule: ReportSchedule): Promise<void> {
    this.busy.set(true);

    try {
      await this.schedules.remove(schedule.reportScheduleId);
      await this.load();
    } catch {
      this.message.set('Could not delete that schedule.');
      this.busy.set(false);
    }
  }

  protected close(): void {
    this.closed.emit();
  }

  private serverMessage(error: unknown): string | null {
    const body = (error as { error?: { message?: string } } | null)?.error;
    return typeof body?.message === 'string' ? body.message : null;
  }
}
