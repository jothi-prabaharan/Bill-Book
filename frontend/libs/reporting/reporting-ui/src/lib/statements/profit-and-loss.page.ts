import { ChangeDetectionStrategy, Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';

interface PLSection {
  accountTypeId: string;
  displayName: string;
  systemName: string;
  totalBalance: number;
  accounts: any[];
}

interface PLResponse {
  reportSection: string;
  sections: PLSection[];
  grossProfit: number;
  netProfit: number;
}

@Component({
  selector: 'bb-profit-and-loss',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './profit-and-loss.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfitAndLossPage implements OnInit {
  private readonly http = inject(HttpClient);
  readonly data = signal<PLResponse | null>(null);
  readonly loading = signal(false);

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.http.get<PLResponse>('/api/statements/profit-and-loss')
      .subscribe({
        next: res => {
          this.data.set(res);
          this.loading.set(false);
        },
        error: err => {
          console.error(err);
          this.loading.set(false);
        }
      });
  }
}
