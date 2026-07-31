import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { API_ROUTES } from '../../core/api/api.routes';
import { Inventory, InventoryInput } from './inventory.models';

@Injectable({ providedIn: 'root' })
export class InventoryApiService {
  private readonly http = inject(HttpClient);

  getInventories(search?: string): Observable<ApiResponse<Inventory[]>> {
    const params = search ? new HttpParams().set('search', search) : undefined;
    return this.http.get<ApiResponse<Inventory[]>>(API_ROUTES.inventories.root, { params });
  }

  createInventory(input: InventoryInput): Observable<ApiResponse<Inventory>> {
    return this.http.post<ApiResponse<Inventory>>(API_ROUTES.inventories.root, input);
  }

  updateInventory(inventoryId: string, input: InventoryInput): Observable<ApiResponse<Inventory>> {
    return this.http.put<ApiResponse<Inventory>>(API_ROUTES.inventories.byId(inventoryId), input);
  }

  deleteInventory(inventoryId: string): Observable<ApiResponse<object>> {
    return this.http.delete<ApiResponse<object>>(API_ROUTES.inventories.byId(inventoryId));
  }
}
