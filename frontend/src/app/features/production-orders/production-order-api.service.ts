import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { API_ROUTES } from '../../core/api/api.routes';
import {
  CompleteProductionOrderInput,
  ProductionOrder,
  ProductionOrderInput,
  ProductionOrderOperation,
  StartProductionOrderInput,
} from './production-order.models';

@Injectable({ providedIn: 'root' })
export class ProductionOrderApiService {
  private readonly http = inject(HttpClient);

  getProductionOrders(search?: string): Observable<ApiResponse<ProductionOrder[]>> {
    const params = search ? new HttpParams().set('search', search) : undefined;
    return this.http.get<ApiResponse<ProductionOrder[]>>(API_ROUTES.productionOrders.root, {
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
