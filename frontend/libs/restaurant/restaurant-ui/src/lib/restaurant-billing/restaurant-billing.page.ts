import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';

export interface BillingRequest {
  contactId: number;
  paymentMode: string;
}

interface OrderInfo {
  restaurantOrderId: number;
  orderNumber: string;
  restaurantTableId: number;
}

interface OrderLineInfo {
  menuItemId: number;
  quantity: number;
  unitPrice: number;
}

@Component({
  selector: 'bb-restaurant-billing',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './restaurant-billing.page.html'
})
export class RestaurantBillingPage implements OnInit {
  private readonly http = inject(HttpClient);

  openOrders: OrderInfo[] = [];
  selectedOrderId: number | null = null;
  orderLines: OrderLineInfo[] = [];
  totalAmount = 0;

  billingRequest: BillingRequest = {
    contactId: 0,
    paymentMode: 'Cash'
  };

  ngOnInit() {
    this.openOrders = [
      { restaurantOrderId: 1, orderNumber: 'RST-101', restaurantTableId: 5 },
      { restaurantOrderId: 2, orderNumber: 'RST-102', restaurantTableId: 2 }
    ];
  }

  selectOrder(id: number) {
    this.selectedOrderId = id;
    this.loadOrderLines();
  }

  loadOrderLines() {
    if (!this.selectedOrderId) {
      this.orderLines = [];
      this.totalAmount = 0;
      return;
    }
    this.orderLines = [
      { menuItemId: 1, quantity: 2, unitPrice: 150 },
      { menuItemId: 3, quantity: 1, unitPrice: 200 }
    ];
    this.totalAmount = this.orderLines.reduce((acc, l) => acc + (l.quantity * l.unitPrice), 0);
  }

  generateBill() {
    if (!this.selectedOrderId) return;
    
    this.http.post('/api/restaurantbilling/' + this.selectedOrderId + '/bill', this.billingRequest).subscribe({
      next: () => {
        alert('Bill Generated Successfully!');
        this.selectedOrderId = null;
        this.loadOrderLines();
      },
      error: (err: { error?: { detail?: string }; message: string }) => {
        alert('Error generating bill: ' + (err.error?.detail || err.message));
      }
    });
  }
}
