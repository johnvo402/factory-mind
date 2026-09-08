import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { API_ROUTES } from '../../core/api/api.routes';
import {
  CompleteProductionOrderInput,
  ProductionOrder,
  ProductionOrderFilters,
  ProductionOrderInput,
  ProductionOrderOperation,
  ProductionOrderPage,
  StartProductionOrderInput,
} from './production-order.models';

@Injectable({ providedIn: 'root' })
export class ProductionOrderApiService {
  private readonly http = inject(HttpClient);

  getProductionOrders(
    filters: ProductionOrderFilters,
    planning = false,
  ): Observable<ApiResponse<ProductionOrderPage>> {
    let params = new HttpParams()
      .set('page', filters.page)
      .set('pageSize', filters.pageSize)
      .set('sortBy', filters.sortBy)
      .set('sortDirection', filters.sortDirection);
    if (filters.search) params = params.set('search', filters.search);
    if (filters.status) params = params.set('status', filters.status);
    if (filters.priority) params = params.set('priority', filters.priority);
    if (filters.deliveryStatus) params = params.set('deliveryStatus', filters.deliveryStatus);
    if (filters.dueFrom) params = params.set('dueFrom', filters.dueFrom);
    if (filters.dueTo) params = params.set('dueTo', filters.dueTo);
    if (filters.productId) params = params.set('productId', filters.productId);
    const route = planning ? API_ROUTES.productionOrders.planning : API_ROUTES.productionOrders.root;
    return this.http.get<ApiResponse<ProductionOrderPage>>(route, {
      params,
    });
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

  releaseProductionOrder(productionOrderId: string): Observable<ApiResponse<ProductionOrder>> {
    return this.http.post<ApiResponse<ProductionOrder>>(
      API_ROUTES.productionOrders.release(productionOrderId),
      null,
    );
  }

  startProductionOrder(
    productionOrderId: string,
    input: StartProductionOrderInput,
  ): Observable<ApiResponse<ProductionOrder>> {
    return this.http.post<ApiResponse<ProductionOrder>>(
      API_ROUTES.productionOrders.start(productionOrderId),
      input,
    );
  }

  completeProductionOrder(
    productionOrderId: string,
    input: CompleteProductionOrderInput,
  ): Observable<ApiResponse<ProductionOrder>> {
    return this.http.post<ApiResponse<ProductionOrder>>(
      API_ROUTES.productionOrders.complete(productionOrderId),
      input,
    );
  }

  cancelProductionOrder(productionOrderId: string): Observable<ApiResponse<ProductionOrder>> {
    return this.http.post<ApiResponse<ProductionOrder>>(
      API_ROUTES.productionOrders.cancel(productionOrderId),
      null,
    );
  }

  getOperations(productionOrderId: string): Observable<ApiResponse<ProductionOrderOperation[]>> {
    return this.http.get<ApiResponse<ProductionOrderOperation[]>>(
      API_ROUTES.productionOrders.operations(productionOrderId),
    );
  }

  startOperation(
    productionOrderId: string,
    operationId: string,
    machineId: string,
  ): Observable<ApiResponse<ProductionOrderOperation>> {
    return this.http.post<ApiResponse<ProductionOrderOperation>>(
      API_ROUTES.productionOrders.startOperation(productionOrderId, operationId),
      { machineId },
    );
  }

  completeOperation(
    productionOrderId: string,
    operationId: string,
  ): Observable<ApiResponse<ProductionOrderOperation>> {
    return this.http.post<ApiResponse<ProductionOrderOperation>>(
      API_ROUTES.productionOrders.completeOperation(productionOrderId, operationId),
      null,
    );
  }
}
