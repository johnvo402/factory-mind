import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { SettingsApiService } from './settings-api.service';
import { SettingsStore } from './settings.store';

describe('SettingsStore', () => {
  let api: jasmine.SpyObj<SettingsApiService>;
  let store: SettingsStore;

  beforeEach(() => {
    api = jasmine.createSpyObj<SettingsApiService>('SettingsApiService', [
      'getCompany', 'updateCompany', 'getUsers', 'createUser', 'updateUser', 'getAi',
      'reindexDocuments',
    ]);
    TestBed.configureTestingModule({
      providers: [SettingsStore, { provide: SettingsApiService, useValue: api }],
    });
    store = TestBed.inject(SettingsStore);
  });

  it('loads company, users, and safe AI metadata together', async () => {
    api.getCompany.and.returnValue(of({
      success: true, message: 'OK', data: { id: 'company-1', name: 'Factory', createdAt: '2026-08-01' },
    }));
    api.getUsers.and.returnValue(of({
      success: true, message: 'OK', data: [{
        id: 'user-1', name: 'Admin', email: 'admin@example.com', role: 'Admin',
        isActive: true, createdAt: '2026-08-01',
      }],
    }));
    api.getAi.and.returnValue(of({
      success: true, message: 'OK', data: {
        provider: 'Google Gemini', chatModel: 'gemini-3.5-flash-lite',
        embeddingModel: 'gemini-embedding-2', embeddingDimensions: 1536,
        maximumOutputTokens: 2048, apiKeyConfigured: true,
      },
    }));

    await store.load();

    expect(store.company()?.name).toBe('Factory');
    expect(store.users().length).toBe(1);
    expect(store.ai()?.apiKeyConfigured).toBeTrue();
    expect(JSON.stringify(store.ai())).not.toContain('apiKey"');
  });

  it('reports the tenant re-index queue count', async () => {
    api.reindexDocuments.and.returnValue(of({
      success: true, message: 'OK', data: { queuedCount: 4 },
    }));

    const queued = await store.reindex();

    expect(queued).toBeTrue();
    expect(store.message()).toContain('4');
  });
});
