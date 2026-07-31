import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { API_ROUTES } from '../../core/api/api.routes';
import { Material, MaterialInput } from './material.models';

@Injectable({ providedIn: 'root' })
export class MaterialApiService {
  private readonly http = inject(HttpClient);

  getMaterials(search?: string): Observable<ApiResponse<Material[]>> {
    const params = search ? new HttpParams().set('search', search) : undefined;
    return this.http.get<ApiResponse<Material[]>>(API_ROUTES.materials.root, { params });
  }

  createMaterial(input: MaterialInput): Observable<ApiResponse<Material>> {
    return this.http.post<ApiResponse<Material>>(API_ROUTES.materials.root, input);
  }

  updateMaterial(materialId: string, input: MaterialInput): Observable<ApiResponse<Material>> {
    return this.http.put<ApiResponse<Material>>(API_ROUTES.materials.byId(materialId), input);
  }

  deleteMaterial(materialId: string): Observable<ApiResponse<object>> {
    return this.http.delete<ApiResponse<object>>(API_ROUTES.materials.byId(materialId));
  }
}
