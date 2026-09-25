import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { readApiFailure } from '@bill-book/api-client';
import { HrmApiService, TeamMember, TeamSummary } from '@bill-book/hrm-core';
import { MessageBoxComponent, UiMessage } from '@bill-book/ui-components';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-team-page',
  standalone: true,
  imports: [CommonModule, MessageBoxComponent],
  templateUrl: './team.page.html',
  styleUrl: '../hrm-page.scss',
})
export class TeamPage implements OnInit {
  private readonly api = inject(HrmApiService);

  protected readonly summary = signal<TeamSummary | null>(null);
  protected readonly members = signal<TeamMember[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    this.busy.set(true);
    try {
      const [sum, mems] = await Promise.all([
        this.api.teamSummary(),
        this.api.teamMembers(),
      ]);
      this.summary.set(sum);
      this.members.set(mems);
    } catch (err) {
      const failure = readApiFailure(err);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.busy.set(false);
    }
  }
}
