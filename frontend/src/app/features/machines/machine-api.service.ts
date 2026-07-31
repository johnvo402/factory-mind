import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { API_ROUTES } from '../../core/api/api.routes';
import { Machine, MachineInput } from './machine.models';

@Injectable({ providedIn: 'root' })
export class MachineApiService {
  private readonly http = inject(HttpClient);

  getMachines(search?: string): Observable<ApiResponse<Machine[]>> {
    const params = search ? new HttpParams().set('search', search) : undefined;
    return this.http.get<ApiResponse<Machine[]>>(API_ROUTES.machines.root, { params });
  }

  createMachine(input: MachineInput): Observable<ApiResponse<Machine>> {
    return this.http.post<ApiResponse<Machine>>(API_ROUTES.machines.root, input);
  }

  updateMachine(machineId: string, input: MachineInput): Observable<ApiResponse<Machine>> {
    return this.http.put<ApiResponse<Machine>>(API_ROUTES.machines.byId(machineId), input);
  }

  deleteMachine(machineId: string): Observable<ApiResponse<object>> {
    return this.http.delete<ApiResponse<object>>(API_ROUTES.machines.byId(machineId));
  }
}
