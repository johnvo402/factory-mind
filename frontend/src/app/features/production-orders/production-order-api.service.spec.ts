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
