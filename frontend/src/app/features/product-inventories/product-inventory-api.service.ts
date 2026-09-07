import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { API_ROUTES } from '../../core/api/api.routes';
import {
  ProductInventoryBalance,
  ProductInventoryBalanceFilters,
  ProductInventoryTransactionFilters,
  ProductInventoryTransactionPage,
} from './product-inventory.models';

@Injectable({ providedIn: 'root' })
export class ProductInventoryApiService {
  private readonly http = inject(HttpClient);

  getBalances(
    filters: ProductInventoryBalanceFilters = {},
  ): Observable<ApiResponse<ProductInventoryBalance[]>> {
    return this.http.get<ApiResponse<ProductInventoryBalance[]>>(
      API_ROUTES.productInventories.root,
      { params: this.balanceParams(filters) },
    );
  }

  getTransactions(
    filters: ProductInventoryTransactionFilters,
  ): Observable<ApiResponse<ProductInventoryTransactionPage>> {
    let params = new HttpParams().set('page', filters.page).set('pageSize', filters.pageSize);
    if (filters.warehouseId) params = params.set('warehouseId', filters.warehouseId);
    if (filters.productId) params = params.set('productId', filters.productId);
    if (filters.transactionType) {
      params = params.set('transactionType', filters.transactionType);
    }
    if (filters.from) params = params.set('from', filters.from);
    if (filters.to) params = params.set('to', filters.to);
    return this.http.get<ApiResponse<ProductInventoryTransactionPage>>(
      API_ROUTES.productInventories.transactions,
      { params },
    );
  }

  private balanceParams(filters: ProductInventoryBalanceFilters): HttpParams {
    let params = new HttpParams();
    if (filters.warehouseId) params = params.set('warehouseId', filters.warehouseId);
    if (filters.productId) params = params.set('productId', filters.productId);
    if (filters.search?.trim()) params = params.set('search', filters.search.trim());
    return params;
  }
}
