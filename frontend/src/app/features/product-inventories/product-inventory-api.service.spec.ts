import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_ROUTES } from '../../core/api/api.routes';
import { ProductInventoryApiService } from './product-inventory-api.service';

describe('ProductInventoryApiService', () => {
  let api: ProductInventoryApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(ProductInventoryApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads filtered finished-goods balances', () => {
    api.getBalances({ warehouseId: 'wh-1', productId: 'p-1', search: ' box ' }).subscribe();
    const request = http.expectOne(
      (candidate) =>
        candidate.url === API_ROUTES.productInventories.root &&
        candidate.params.get('warehouseId') === 'wh-1' &&
        candidate.params.get('productId') === 'p-1' &&
        candidate.params.get('search') === 'box',
    );
    expect(request.request.method).toBe('GET');
    request.flush({ success: true, data: [] });
  });

  it('loads paged and filtered finished-goods transactions', () => {
    api
      .getTransactions({
        warehouseId: 'wh-1',
        productId: 'p-1',
        transactionType: 'ProductionOutput',
        from: '2026-01-01T00:00:00.000Z',
        to: '2026-01-31T23:59:59.999Z',
        page: 2,
        pageSize: 25,
      })
      .subscribe();
    const request = http.expectOne(
      API_ROUTES.productInventories.transactions +
        '?page=2&pageSize=25&warehouseId=wh-1&productId=p-1&transactionType=ProductionOutput' +
        '&from=2026-01-01T00:00:00.000Z&to=2026-01-31T23:59:59.999Z',
    );
    expect(request.request.method).toBe('GET');
    request.flush({ success: true, data: { items: [], page: 2, pageSize: 25, totalCount: 0 } });
  });
});
