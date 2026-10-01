import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';

export interface RestaurantOrder {
  restaurantOrderId: number;
  orderNumber: string;
  orderType: number;
  restaurantTableId?: number;
  waiterUserId?: string;
  contactId?: number;
  status: number;
  totalAmount: number;
}

export interface RestaurantOrderDetail {
  restaurantOrderDetailId: number;
  restaurantOrderId: number;
  menuItemId: number;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
}

@Component({
  selector: 'bb-restaurant-order',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './restaurant-order.page.html'
})
export class RestaurantOrderPage {
  private readonly http = inject(HttpClient);
  
  menuItems = [
    { menuItemId: 1, price: 100 },
    { menuItemId: 2, price: 150 },
    { menuItemId: 3, price: 200 }
  ];

  currentOrder: RestaurantOrder | null = null;
  orderLines: RestaurantOrderDetail[] = [];

  openNewOrder() {
    const draft = {
      orderType: 1, // DineIn
      status: 1, // Open
      totalAmount: 0
    };
    this.http.post<RestaurantOrder>('/api/restaurantorder', draft).subscribe(res => {
      this.currentOrder = res;
    });
  }

  addItemToOrder(item: { menuItemId: number; price: number }) {
    if (!this.currentOrder) return;
    
    const line = {
      menuItemId: item.menuItemId,
      quantity: 1,
      unitPrice: item.price
    };
    
    this.http.post('/api/restaurantorder/' + this.currentOrder.restaurantOrderId + '/lines', line).subscribe(() => {
      this.orderLines.push({
        restaurantOrderDetailId: Date.now(), 
        restaurantOrderId: this.currentOrder!.restaurantOrderId,
        menuItemId: item.menuItemId,
        quantity: 1,
        unitPrice: item.price,
        totalPrice: item.price
      });
      this.currentOrder!.totalAmount += item.price;
    });
  }

  removeLine(line: RestaurantOrderDetail) {
    if (!this.currentOrder) return;
    
    this.http.delete('/api/restaurantorder/' + this.currentOrder.restaurantOrderId + '/lines/' + line.restaurantOrderDetailId).subscribe(() => {
      this.orderLines = this.orderLines.filter(l => l !== line);
      this.currentOrder!.totalAmount -= line.totalPrice;
    });
  }
  
  updateOrderType(newType: number) {
    if (!this.currentOrder) return;
    this.currentOrder.orderType = newType;
    this.http.put('/api/restaurantorder/' + this.currentOrder.restaurantOrderId + '/type', newType).subscribe();
  }
}
