import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { IssuePart, SaveWorkOrder, StockItem, StockWarehouse, WorkOrder, WorkOrderActionBody, WorkOrderStatus } from './work-order.models';

type Saved = { id: number };

/** The WorkOrder service's routes (S6, TK-66), behind the Gateway's `/api/work-orders`. */
@Injectable({ providedIn: 'root' })
export class WorkOrderApiService {
  private readonly http = inject(HttpClient);

  list(status?: WorkOrderStatus | null): Promise<WorkOrder[]> {
    return firstValueFrom(this.http.get<WorkOrder[]>('/api/work-orders', { params: status ? { status } : {} }));
  }

  get(id: number): Promise<WorkOrder> {
    return firstValueFrom(this.http.get<WorkOrder>(`/api/work-orders/${id}`));
  }

  save(id: number | null, body: SaveWorkOrder): Promise<Saved> {
    return firstValueFrom(id === null ? this.http.post<Saved>('/api/work-orders', body) : this.http.put<Saved>(`/api/work-orders/${id}`, body));
  }

  act(id: number, body: WorkOrderActionBody): Promise<Saved> {
    return firstValueFrom(this.http.post<Saved>(`/api/work-orders/${id}/actions`, body));
  }

  close(id: number): Promise<Saved> {
    return firstValueFrom(this.http.post<Saved>(`/api/work-orders/${id}/close`, {}));
  }

  tick(id: number, taskId: number, isDone: boolean): Promise<Saved> {
    return firstValueFrom(this.http.put<Saved>(`/api/work-orders/${id}/tasks/${taskId}`, { isDone }));
  }

  issuePart(id: number, body: IssuePart): Promise<Saved> {
    return firstValueFrom(this.http.post<Saved>(`/api/work-orders/${id}/parts`, body));
  }

  stockItems(search?: string): Promise<StockItem[]> {
    return firstValueFrom(this.http.get<StockItem[]>('/api/work-orders/stock-items', { params: search ? { search } : {} }));
  }

  warehouses(): Promise<StockWarehouse[]> {
    return firstValueFrom(this.http.get<StockWarehouse[]>('/api/work-orders/warehouses'));
  }
}
