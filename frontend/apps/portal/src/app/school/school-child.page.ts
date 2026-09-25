import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { SchoolPortalApi } from './school-portal-api.service';
import { PortalAttendance, PortalChild, PortalExam, attendancePercent } from './school-portal.models';

/** One child in the parent portal (S9, TK-69): a month of attendance and the marks of published exams. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-school-child',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './school-child.page.html',
  styleUrl: './school-portal.scss',
})
export class SchoolChildPage implements OnInit {
  private readonly api = inject(SchoolPortalApi);
  private readonly route = inject(ActivatedRoute);

  protected readonly child = signal<PortalChild | null>(null);
  protected readonly attendance = signal<PortalAttendance | null>(null);
  protected readonly exams = signal<PortalExam[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly percent = computed(() => {
    const a = this.attendance();
    return a ? attendancePercent(a) : null;
  });

  protected month = new Date().toISOString().slice(0, 7);
  private studentId = 0;

  ngOnInit(): void {
    this.studentId = Number(this.route.snapshot.paramMap.get('studentId'));
    void this.load();
  }

  protected async loadMonth(): Promise<void> {
    try {
      this.attendance.set(await this.api.attendance(this.studentId, `${this.month}-01`));
    } catch {
      this.error.set('Attendance could not be loaded.');
    }
  }

  private async load(): Promise<void> {
    try {
      const [children, exams] = await Promise.all([this.api.children(), this.api.marks(this.studentId)]);
      this.child.set(children.find((c) => c.studentId === this.studentId) ?? null);
      this.exams.set(exams);
      await this.loadMonth();
    } catch {
      this.error.set('This child could not be found among yours.');
    }
  }
}
