import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { API_ROUTES } from '../../core/api/api.routes';
import { Bom, BomInput, MaterialRequirements } from './bom.models';

@Injectable({ providedIn: 'root' })
export class BomApiService {
  private readonly http = inject(HttpClient);

  getBoms(productId: string): Observable<ApiResponse<Bom[]>> {
    return this.http.get<ApiResponse<Bom[]>>(API_ROUTES.products.boms(productId));
  }

  createBom(productId: string, input: BomInput): Observable<ApiResponse<Bom>> {
    return this.http.post<ApiResponse<Bom>>(API_ROUTES.products.boms(productId), input);
  }

  updateBom(productId: string, bomId: string, input: BomInput): Observable<ApiResponse<Bom>> {
    return this.http.put<ApiResponse<Bom>>(API_ROUTES.products.bomById(productId, bomId), input);
  }

  activateBom(productId: string, bomId: string): Observable<ApiResponse<Bom>> {
    return this.http.post<ApiResponse<Bom>>(
      API_ROUTES.products.activateBom(productId, bomId),
      {},
    );
  }

  archiveBom(productId: string, bomId: string): Observable<ApiResponse<Bom>> {
    return this.http.post<ApiResponse<Bom>>(
      API_ROUTES.products.archiveBom(productId, bomId),
      {},
    );
  }

  getProductRequirements(
    productId: string,
    quantity: number,
  ): Observable<ApiResponse<MaterialRequirements>> {
    const params = new HttpParams().set('quantity', quantity);
    return this.http.get<ApiResponse<MaterialRequirements>>(
      API_ROUTES.products.materialRequirements(productId),
      { params },
    );
  }

  getProductionOrderRequirements(
    productionOrderId: string,
  ): Observable<ApiResponse<MaterialRequirements>> {
    return this.http.get<ApiResponse<MaterialRequirements>>(
      API_ROUTES.productionOrders.materialRequirements(productionOrderId),
    );
  }
}
