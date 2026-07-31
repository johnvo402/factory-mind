import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { API_ROUTES } from '../../core/api/api.routes';
import { ProductionOrder, ProductionOrderInput } from './production-order.models';

@Injectable({ providedIn: 'root' })
export class ProductionOrderApiService {
  private readonly http = inject(HttpClient);

  getProductionOrders(search?: string): Observable<ApiResponse<ProductionOrder[]>> {
    const params = search ? new HttpParams().set('search', search) : undefined;
    return this.http.get<ApiResponse<ProductionOrder[]>>(API_ROUTES.productionOrders.root, { params });
  }

  createProductionOrder(input: ProductionOrderInput): Observable<ApiResponse<ProductionOrder>> {
    return this.http.post<ApiResponse<ProductionOrder>>(API_ROUTES.productionOrders.root, input);
  }

  updateProductionOrder(
    productionOrderId: string,
    input: ProductionOrderInput,
  ): Observable<ApiResponse<ProductionOrder>> {
    return this.http.put<ApiResponse<ProductionOrder>>(
      API_ROUTES.productionOrders.byId(productionOrderId),
      input,
    );
  }

  deleteProductionOrder(productionOrderId: string): Observable<ApiResponse<object>> {
    return this.http.delete<ApiResponse<object>>(
      API_ROUTES.productionOrders.byId(productionOrderId),
    );
  }
}
