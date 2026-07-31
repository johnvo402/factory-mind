import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { KnowledgeApiService } from './knowledge-api.service';
import { KnowledgeStore } from './knowledge.store';

describe('KnowledgeStore', () => {
  let api: jasmine.SpyObj<KnowledgeApiService>;
  let store: KnowledgeStore;

  beforeEach(() => {
    api = jasmine.createSpyObj<KnowledgeApiService>('KnowledgeApiService', [
      'getDocuments', 'upload', 'process', 'search',
    ]);
    TestBed.configureTestingModule({
      providers: [KnowledgeStore, { provide: KnowledgeApiService, useValue: api }],
    });
    store = TestBed.inject(KnowledgeStore);
  });

  it('loads tenant documents and keeps processing metadata', async () => {
    api.getDocuments.and.returnValue(of({
      success: true,
      message: 'OK',
      data: [{
        id: 'doc-1', title: 'SOP', fileName: 'sop.pdf', contentType: 'application/pdf',
        size: 1200, status: 'ready', pageCount: 4, chunkCount: 8, processingError: null,
        createdAt: '2026-08-01', processedAt: '2026-08-01',
      }],
    }));

    await store.load();

    expect(store.documents()[0].status).toBe('ready');
    expect(store.documents()[0].chunkCount).toBe(8);
  });

  it('stores semantic search score and page for source inspection', async () => {
    api.search.and.returnValue(of({
      success: true,
      message: 'OK',
      data: [{
        documentId: 'doc-1', documentTitle: 'SOP', fileName: 'sop.pdf', chunkId: 'chunk-1',
        pageNumber: 3, content: 'Safety instruction', score: 0.91,
      }],
    }));

    await store.search(' safety ');

    expect(api.search).toHaveBeenCalledWith('safety');
    expect(store.results()[0].pageNumber).toBe(3);
    expect(store.results()[0].score).toBe(0.91);
  });
});
