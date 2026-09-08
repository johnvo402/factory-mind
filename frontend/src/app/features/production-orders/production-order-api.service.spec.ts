import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_ROUTES } from '../../core/api/api.routes';
import { ProductionOrderApiService } from './production-order-api.service';

describe('ProductionOrderApiService', () => {
  let api: ProductionOrderApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(ProductionOrderApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends typed planning filters and bounded pagination to the planning endpoint', () => {
    api
      .getProductionOrders(
        {
          search: 'PO-001',
          status: 'planned',
          priority: 'urgent',
          deliveryStatus: 'overdue',
          dueFrom: '2026-09-01T00:00:00Z',
          dueTo: '2026-09-30T23:59:59Z',
          productId: 'product-1',
          page: 2,
          pageSize: 25,
          sortBy: 'dueDate',
          sortDirection: 'asc',
        },
        true,
      )
      .subscribe();

    const request = http.expectOne(
      (candidate) => candidate.url === API_ROUTES.productionOrders.planning,
    );
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('priority')).toBe('urgent');
    expect(request.request.params.get('deliveryStatus')).toBe('overdue');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(request.request.params.get('sortBy')).toBe('dueDate');
    expect(request.request.params.get('sortDirection')).toBe('asc');
    expect(request.request.params.get('productId')).toBe('product-1');
    request.flush({ success: true, data: { items: [], page: 2, pageSize: 25, totalCount: 0 } });
  });

  it('posts release to the canonical URL', () => {
    api.releaseProductionOrder('po-1').subscribe();
    const request = http.expectOne(API_ROUTES.productionOrders.release('po-1'));
    expect(request.request.method).toBe('POST');
    request.flush({ success: true, data: null });
  });

  it('posts exact start allocations', () => {
    const input = {
      allocations: [{ materialId: 'mat-1', warehouseId: 'wh-1', quantity: 2.5 }],
    };
    api.startProductionOrder('po-1', input).subscribe();
    const request = http.expectOne(API_ROUTES.productionOrders.start('po-1'));
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(input);
    request.flush({ success: true, data: null });
  });

  it('posts the finished-goods destination on completion', () => {
    api.completeProductionOrder('po-1', { warehouseId: 'wh-fg' }).subscribe();
    const request = http.expectOne(API_ROUTES.productionOrders.complete('po-1'));
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ warehouseId: 'wh-fg' });
    request.flush({ success: true, data: null });
  });

  it('posts cancel and loads dedicated operations', () => {
    api.cancelProductionOrder('po-1').subscribe();
    const cancel = http.expectOne(API_ROUTES.productionOrders.cancel('po-1'));
    expect(cancel.request.method).toBe('POST');
    cancel.flush({ success: true, data: null });

    api.getOperations('po-1').subscribe();
    const operations = http.expectOne(API_ROUTES.productionOrders.operations('po-1'));
    expect(operations.request.method).toBe('GET');
    operations.flush({ success: true, data: [] });
  });
});
