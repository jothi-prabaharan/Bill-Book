import { ChangeDetectionStrategy, Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';

interface BSSection {
  accountTypeId: string;
  displayName: string;
  systemName: string;
  totalBalance: number;
  accounts: any[];
}

interface BSResponse {
  reportSection: string;
  sections: BSSection[];
}

@Component({
  selector: 'bb-balance-sheet',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './balance-sheet.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BalanceSheetPage implements OnInit {
  private readonly http = inject(HttpClient);
  readonly data = signal<BSResponse | null>(null);
  readonly loading = signal(false);

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.http.get<BSResponse>('/api/statements/balance-sheet')
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
