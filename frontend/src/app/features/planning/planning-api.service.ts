import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { API_ROUTES } from '../../core/api/api.routes';
import { SchedulePreview } from './planning.models';

@Injectable({ providedIn: 'root' })
export class PlanningApiService {
  private readonly http = inject(HttpClient);

  getPreview(horizonDays: number): Observable<ApiResponse<SchedulePreview>> {
    const params = new HttpParams().set('horizonDays', horizonDays);
    return this.http.get<ApiResponse<SchedulePreview>>(API_ROUTES.productionOrders.schedulePreview, { params });
  }
}
