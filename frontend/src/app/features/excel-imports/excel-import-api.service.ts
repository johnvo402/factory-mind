import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { API_ROUTES } from '../../core/api/api.routes';
import { ExcelImportEntityType, ExcelImportResult, ExcelPreview } from './excel-import.models';

@Injectable({ providedIn: 'root' })
export class ExcelImportApiService {
  private readonly http = inject(HttpClient);

  preview(
    entityType: ExcelImportEntityType,
    file: File,
  ): Observable<ApiResponse<ExcelPreview>> {
    const form = new FormData();
    form.append('entityType', entityType);
    form.append('file', file);
    return this.http.post<ApiResponse<ExcelPreview>>(API_ROUTES.excelImports.preview, form);
  }

  import(
    entityType: ExcelImportEntityType,
    mapping: Record<string, string>,
    file: File,
  ): Observable<ApiResponse<ExcelImportResult>> {
    const form = new FormData();
    form.append('entityType', entityType);
    form.append('mapping', JSON.stringify(mapping));
    form.append('file', file);
    return this.http.post<ApiResponse<ExcelImportResult>>(API_ROUTES.excelImports.import, form);
  }
}
