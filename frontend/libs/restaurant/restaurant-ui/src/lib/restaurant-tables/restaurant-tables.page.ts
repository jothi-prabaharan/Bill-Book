import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';

export interface RestaurantTable {
  restaurantTableId: number;
  number: string;
  capacity: number;
  status: number; // 1 = Free, 2 = Occupied, 3 = Billed
}

@Component({
  selector: 'bb-restaurant-tables',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './restaurant-tables.page.html'
})
export class RestaurantTablesPage implements OnInit {
  private readonly http = inject(HttpClient);
  tables: RestaurantTable[] = [];

  ngOnInit() {
    this.loadTables();
  }

  loadTables() {
    this.http.get<RestaurantTable[]>('/api/restauranttable').subscribe(data => {
      this.tables = data;
    });
  }

  updateStatus(table: RestaurantTable) {
    const nextStatus = table.status === 1 ? 2 : table.status === 2 ? 3 : 1;
    this.http.put('/api/restauranttable/' + table.restaurantTableId + '/status', nextStatus).subscribe(() => {
      table.status = nextStatus;
    });
  }
}
