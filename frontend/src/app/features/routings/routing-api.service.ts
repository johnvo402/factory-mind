import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { API_ROUTES } from '../../core/api/api.routes';
import { Routing, RoutingInput } from './routing.models';

@Injectable({ providedIn: 'root' })
export class RoutingApiService {
  private readonly http = inject(HttpClient);
  list(productId: string): Observable<ApiResponse<Routing[]>> {
    return this.http.get<ApiResponse<Routing[]>>(API_ROUTES.products.routings(productId));
  }
  create(productId: string, input: RoutingInput): Observable<ApiResponse<Routing>> {
    return this.http.post<ApiResponse<Routing>>(API_ROUTES.products.routings(productId), input);
  }
  update(productId: string, routingId: string, input: RoutingInput): Observable<ApiResponse<Routing>> {
    return this.http.put<ApiResponse<Routing>>(
      API_ROUTES.products.routingById(productId, routingId), input,
    );
  }
  activate(productId: string, routingId: string): Observable<ApiResponse<Routing>> {
    return this.http.post<ApiResponse<Routing>>(
      API_ROUTES.products.activateRouting(productId, routingId), null,
    );
  }
}
