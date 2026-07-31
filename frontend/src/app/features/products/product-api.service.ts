import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { API_ROUTES } from '../../core/api/api.routes';
import { Product, ProductInput } from './product.models';

@Injectable({ providedIn: 'root' })
export class ProductApiService {
  private readonly http = inject(HttpClient);

  getProducts(search?: string): Observable<ApiResponse<Product[]>> {
    const params = search ? new HttpParams().set('search', search) : undefined;
    return this.http.get<ApiResponse<Product[]>>(API_ROUTES.products.root, { params });
  }

  createProduct(input: ProductInput): Observable<ApiResponse<Product>> {
    return this.http.post<ApiResponse<Product>>(API_ROUTES.products.root, input);
  }

  updateProduct(productId: string, input: ProductInput): Observable<ApiResponse<Product>> {
    return this.http.put<ApiResponse<Product>>(API_ROUTES.products.byId(productId), input);
  }

  deleteProduct(productId: string): Observable<ApiResponse<object>> {
    return this.http.delete<ApiResponse<object>>(API_ROUTES.products.byId(productId));
  }
}
