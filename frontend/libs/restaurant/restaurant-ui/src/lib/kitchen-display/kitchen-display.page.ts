import { Component, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';

export interface KotLine {
  kotLineId: number;
  menuItemId: number;
  quantity: number;
  notes?: string;
  status: number; // 1 = Sent, 2 = Preparing, 3 = Ready, 4 = Served, 5 = Cancelled
}

export interface KotTicket {
  kotId: number;
  kotNumber: string;
  restaurantTableId?: number;
  lines: KotLine[];
}

@Component({
  selector: 'bb-kitchen-display',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './kitchen-display.page.html'
})
export class KitchenDisplayPage implements OnInit, OnDestroy {
  private readonly http = inject(HttpClient);
  
  currentStation = 1; // Default to Hot Kitchen
  tickets: KotTicket[] = [];
  pollTimer: any;

  ngOnInit() {
    this.loadTickets();
    this.pollTimer = setInterval(() => this.loadTickets(), 5000); // Poll every 5s
  }

  ngOnDestroy() {
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
    }
  }

  setStation(station: number) {
    this.currentStation = station;
    this.loadTickets();
  }

  loadTickets() {
    this.http.get<KotTicket[]>('/api/kot/station/' + this.currentStation).subscribe({
      next: (data) => {
        this.tickets = data;
      },
      error: () => {
        // Ignored for demo
      }
    });
  }

  updateStatus(line: KotLine, newStatus: number) {
    this.http.put('/api/kot/lines/' + line.kotLineId + '/status', newStatus).subscribe(() => {
      line.status = newStatus;
    });
  }
}
