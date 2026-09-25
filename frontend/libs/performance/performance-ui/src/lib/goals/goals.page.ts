import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Goal, PerformanceApiService, ReviewCycle } from '@bill-book/performance-core';

@Component({
  selector: 'bb-performance-goals',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './goals.page.html',
  styleUrl: '../performance-page.scss',
})
export class GoalsPage implements OnInit {
  private readonly api = inject(PerformanceApiService);

  readonly cycles = signal<ReviewCycle[]>([]);
  readonly selectedCycleId = signal<number>(0);
  readonly employeeId = signal<number>(1);
  readonly goals = signal<Goal[]>([]);
  readonly loading = signal(false);
  readonly showForm = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = signal({
    title: '',
    description: '',
    weightage: 25,
    measure: '',
    target: '',
  });

  ngOnInit(): void {
    void this.loadCycles();
  }

  async loadCycles(): Promise<void> {
    try {
      const cycs = await this.api.getCycles();
      this.cycles.set(cycs);
      if (cycs.length > 0) {
        this.selectedCycleId.set(cycs[0].reviewCycleId);
        await this.loadGoals();
      }
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to load cycles');
    }
  }

  async loadGoals(): Promise<void> {
    const cId = this.selectedCycleId();
    const eId = this.employeeId();
    if (!cId || !eId) return;

    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const list = await this.api.getGoals(cId, eId);
      this.goals.set(list);
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to load goals');
    } finally {
      this.loading.set(false);
    }
  }

  totalWeightage(): number {
    return this.goals().reduce((sum, g) => sum + g.weightage, 0);
  }

  openCreate(): void {
    this.showForm.set(true);
  }

  async saveGoal(): Promise<void> {
    try {
      await this.api.saveGoal({
        reviewCycleId: this.selectedCycleId(),
        employeeId: this.employeeId(),
        ...this.form(),
      });
      this.showForm.set(false);
      await this.loadGoals();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to save goal');
    }
  }
}
