import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { API_ROUTES } from '../../core/api/api.routes';
import { KnowledgeDocument, KnowledgeSearchResult } from './knowledge.models';

@Injectable({ providedIn: 'root' })
export class KnowledgeApiService {
  private readonly http = inject(HttpClient);

  getDocuments(): Observable<ApiResponse<KnowledgeDocument[]>> {
    return this.http.get<ApiResponse<KnowledgeDocument[]>>(API_ROUTES.documents.root);
  }

  upload(file: File, title?: string): Observable<ApiResponse<KnowledgeDocument>> {
    const form = new FormData();
    form.append('file', file);
    if (title?.trim()) {
      form.append('title', title.trim());
    }
    return this.http.post<ApiResponse<KnowledgeDocument>>(API_ROUTES.documents.root, form);
  }

  process(documentId: string): Observable<ApiResponse<KnowledgeDocument>> {
    return this.http.post<ApiResponse<KnowledgeDocument>>(API_ROUTES.documents.process(documentId), {});
  }

  search(query: string, limit = 5): Observable<ApiResponse<KnowledgeSearchResult[]>> {
    return this.http.post<ApiResponse<KnowledgeSearchResult[]>>(API_ROUTES.knowledge.search, {
      query,
      limit,
    });
  }
}
