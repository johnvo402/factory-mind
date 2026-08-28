import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { API_ROUTES } from '../../core/api/api.routes';
import {
  Inventory,
  InventoryAdjustmentInput,
  InventoryMovementInput,
  InventoryTransaction,
  InventoryTransactionPage,
  InventoryTransferInput,
  Warehouse,
  WarehouseCreateInput,
  WarehouseUpdateInput,
} from './inventory.models';

@Injectable({ providedIn: 'root' })
export class InventoryApiService {
  private readonly http = inject(HttpClient);

  getInventories(search?: string): Observable<ApiResponse<Inventory[]>> {
    const params = search ? new HttpParams().set('search', search) : undefined;
    return this.http.get<ApiResponse<Inventory[]>>(API_ROUTES.inventories.root, { params });
  }

  getTransactions(page = 1, pageSize = 50): Observable<ApiResponse<InventoryTransactionPage>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<ApiResponse<InventoryTransactionPage>>(
      API_ROUTES.inventories.transactions,
      { params },
    );
  }

  receive(input: InventoryMovementInput): Observable<ApiResponse<InventoryTransaction>> {
    return this.http.post<ApiResponse<InventoryTransaction>>(API_ROUTES.inventories.receive, input);
  }

  issue(input: InventoryMovementInput): Observable<ApiResponse<InventoryTransaction>> {
    return this.http.post<ApiResponse<InventoryTransaction>>(API_ROUTES.inventories.issue, input);
  }

  adjust(input: InventoryAdjustmentInput): Observable<ApiResponse<InventoryTransaction>> {
    return this.http.post<ApiResponse<InventoryTransaction>>(API_ROUTES.inventories.adjust, input);
  }

  transfer(input: InventoryTransferInput): Observable<ApiResponse<InventoryTransaction[]>> {
    return this.http.post<ApiResponse<InventoryTransaction[]>>(
      API_ROUTES.inventories.transfer,
      input,
    );
  }

  getWarehouses(search?: string): Observable<ApiResponse<Warehouse[]>> {
    const params = search ? new HttpParams().set('search', search) : undefined;
    return this.http.get<ApiResponse<Warehouse[]>>(API_ROUTES.warehouses.root, { params });
  }

  createWarehouse(input: WarehouseCreateInput): Observable<ApiResponse<Warehouse>> {
    return this.http.post<ApiResponse<Warehouse>>(API_ROUTES.warehouses.root, input);
  }

  updateWarehouse(
    warehouseId: string,
    input: WarehouseUpdateInput,
  ): Observable<ApiResponse<Warehouse>> {
    return this.http.put<ApiResponse<Warehouse>>(API_ROUTES.warehouses.byId(warehouseId), input);
  }

  deactivateWarehouse(warehouseId: string): Observable<ApiResponse<object>> {
    return this.http.delete<ApiResponse<object>>(API_ROUTES.warehouses.byId(warehouseId));
  }
}
