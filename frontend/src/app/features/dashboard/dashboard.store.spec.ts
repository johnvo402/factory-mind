import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { DashboardApiService } from './dashboard-api.service';
import { DashboardSummary } from './dashboard.models';
import { DashboardStore } from './dashboard.store';

describe('DashboardStore', () => {
  let api: jasmine.SpyObj<DashboardApiService>;
  let store: DashboardStore;

  beforeEach(() => {
    api = jasmine.createSpyObj<DashboardApiService>('DashboardApiService', ['getSummary']);
    TestBed.configureTestingModule({
      providers: [DashboardStore, { provide: DashboardApiService, useValue: api }],
    });
    store = TestBed.inject(DashboardStore);
  });

  it('loads the tenant dashboard summary', async () => {
    const summary: DashboardSummary = {
      activeOrders: 3,
      inventoryBalances: 5,
      availableMachines: 2,
      totalMachines: 4,
      alerts: 1,
    };
    api.getSummary.and.returnValue(of({ success: true, message: 'OK', data: summary }));

    await store.load();

    expect(store.summary()).toEqual(summary);
    expect(store.error()).toBe('');
  });

  it('keeps an isolated retryable error when the dashboard fails', async () => {
    api.getSummary.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 503,
      error: { detail: 'Dashboard is temporarily unavailable.' },
    })));

    await store.load();

    expect(store.summary()).toBeNull();
    expect(store.error()).toBe('Dashboard is temporarily unavailable.');
  });
});
