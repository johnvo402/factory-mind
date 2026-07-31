import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { API_ROUTES } from '../../core/api/api.routes';
import {
  AiSettings,
  CompanySettings,
  CreateUserInput,
  UpdateUserInput,
  UserSettings,
} from './settings.models';

@Injectable({ providedIn: 'root' })
export class SettingsApiService {
  private readonly http = inject(HttpClient);

  getCompany(): Observable<ApiResponse<CompanySettings>> {
    return this.http.get<ApiResponse<CompanySettings>>(API_ROUTES.settings.company);
  }

  updateCompany(name: string): Observable<ApiResponse<CompanySettings>> {
    return this.http.put<ApiResponse<CompanySettings>>(API_ROUTES.settings.company, { name });
  }

  getUsers(): Observable<ApiResponse<UserSettings[]>> {
    return this.http.get<ApiResponse<UserSettings[]>>(API_ROUTES.settings.users);
  }

  createUser(input: CreateUserInput): Observable<ApiResponse<UserSettings>> {
    return this.http.post<ApiResponse<UserSettings>>(API_ROUTES.settings.users, input);
  }

  updateUser(userId: string, input: UpdateUserInput): Observable<ApiResponse<UserSettings>> {
    return this.http.put<ApiResponse<UserSettings>>(API_ROUTES.settings.userById(userId), input);
  }

  getAi(): Observable<ApiResponse<AiSettings>> {
    return this.http.get<ApiResponse<AiSettings>>(API_ROUTES.settings.ai);
  }

  reindexDocuments(): Observable<ApiResponse<{ queuedCount: number }>> {
    return this.http.post<ApiResponse<{ queuedCount: number }>>(API_ROUTES.documents.reindex, {});
  }
}
