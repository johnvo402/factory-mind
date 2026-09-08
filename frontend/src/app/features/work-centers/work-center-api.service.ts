import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { API_ROUTES } from '../../core/api/api.routes';
import { WorkCenter, WorkCenterCalendar, WorkCenterCalendarInput, WorkCenterInput } from './work-center.models';

@Injectable({ providedIn: 'root' })
export class WorkCenterApiService {
  private readonly http = inject(HttpClient);

  getWorkCenters(search?: string): Observable<ApiResponse<WorkCenter[]>> {
    const params = search ? new HttpParams().set('search', search) : undefined;
    return this.http.get<ApiResponse<WorkCenter[]>>(API_ROUTES.workCenters.root, { params });
  }

  create(input: WorkCenterInput): Observable<ApiResponse<WorkCenter>> {
    return this.http.post<ApiResponse<WorkCenter>>(API_ROUTES.workCenters.root, input);
  }

  update(id: string, input: WorkCenterInput): Observable<ApiResponse<WorkCenter>> {
    return this.http.put<ApiResponse<WorkCenter>>(API_ROUTES.workCenters.byId(id), input);
  }

  deactivate(id: string): Observable<ApiResponse<WorkCenter>> {
    return this.http.post<ApiResponse<WorkCenter>>(API_ROUTES.workCenters.deactivate(id), null);
  }

  getCalendar(id: string): Observable<ApiResponse<WorkCenterCalendar>> {
    return this.http.get<ApiResponse<WorkCenterCalendar>>(API_ROUTES.workCenters.calendar(id));
  }

  replaceCalendar(id: string, input: WorkCenterCalendarInput): Observable<ApiResponse<WorkCenterCalendar>> {
    return this.http.put<ApiResponse<WorkCenterCalendar>>(API_ROUTES.workCenters.calendar(id), input);
  }
}
