import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { InventoryApiService } from '../inventories/inventory-api.service';
import { ProductApiService } from '../products/product-api.service';
import { ProductInventoryApiService } from './product-inventory-api.service';
import {
  ProductInventoryBalance,
  ProductInventoryTransactionPage,
} from './product-inventory.models';
import { ProductInventoryWorkspaceComponent } from './product-inventory-workspace.component';

describe('ProductInventoryWorkspaceComponent', () => {
  let fixture: ComponentFixture<ProductInventoryWorkspaceComponent>;
  let api: jasmine.SpyObj<ProductInventoryApiService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<ProductInventoryApiService>('ProductInventoryApiService', [
      'getBalances',
      'getTransactions',
    ]);
    const products = jasmine.createSpyObj<ProductApiService>('ProductApiService', ['getProducts']);
    const inventories = jasmine.createSpyObj<InventoryApiService>('InventoryApiService', [
      'getWarehouses',
    ]);
    api.getBalances.and.returnValue(success([balance()]));
    api.getTransactions.and.returnValue(success(transactionPage()));
    products.getProducts.and.returnValue(
      success([
        {
          id: 'p-1',
          code: 'P-001',
          name: 'Storage Box',
          createdAt: '',
          updatedAt: '',
        },
      ]),
    );
    inventories.getWarehouses.and.returnValue(
      success([
        {
          id: 'wh-1',
          code: 'WH-FG',
          name: 'Finished Goods',
          description: null,
          isActive: true,
          createdAt: '',
          updatedAt: '',
        },
      ]),
    );
    TestBed.configureTestingModule({
      providers: [
        { provide: ProductInventoryApiService, useValue: api },
        { provide: ProductApiService, useValue: products },
        { provide: InventoryApiService, useValue: inventories },
      ],
    });
    fixture = TestBed.createComponent(ProductInventoryWorkspaceComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('renders finished-goods product, warehouse, and quantity', () => {
    expect(fixture.nativeElement.textContent).toContain('P-001');
    expect(fixture.nativeElement.textContent).toContain('WH-FG');
    expect(fixture.nativeElement.textContent).toContain('12.5');
  });

  it('loads paged transaction history and renders ProductionOutput in Vietnamese', async () => {
    const button = (
      Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[]
    ).find((item) => item.textContent?.trim() === 'Lịch sử thành phẩm');
    button?.click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(api.getTransactions).toHaveBeenCalledWith(
      jasmine.objectContaining({ page: 1, pageSize: 25 }),
    );
    expect(fixture.nativeElement.textContent).toContain('Nhập thành phẩm từ sản xuất');
    expect(fixture.nativeElement.textContent).toContain('Trang 1 / 2');
  });

  it('applies ledger filters and requests the next page', async () => {
    clickButton('Lịch sử thành phẩm');
    await fixture.whenStable();
    fixture.detectChanges();
    const selects = Array.from(
      fixture.nativeElement.querySelectorAll('.history-panel select'),
    ) as HTMLSelectElement[];
    setSelect(selects[0], 'wh-1');
    setSelect(selects[1], 'p-1');
    setSelect(selects[2], 'ProductionOutput');
    fixture.detectChanges();
    clickButton('Áp dụng');
    await fixture.whenStable();
    expect(api.getTransactions).toHaveBeenCalledWith({
      warehouseId: 'wh-1',
      productId: 'p-1',
      transactionType: 'ProductionOutput',
      from: undefined,
      to: undefined,
      page: 1,
      pageSize: 25,
    });

    clickButton('Trang sau');
    await fixture.whenStable();
    expect(api.getTransactions).toHaveBeenCalledWith(
      jasmine.objectContaining({ page: 2, pageSize: 25 }),
    );
  });

  function clickButton(text: string): void {
    const button = (
      Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[]
    ).find((item) => item.textContent?.trim() === text);
    expect(button).withContext(`Expected button ${text}`).toBeDefined();
    button?.click();
  }

  function setSelect(select: HTMLSelectElement, value: string): void {
    select.value = value;
    select.dispatchEvent(new Event('change'));
  }

  function balance(): ProductInventoryBalance {
    return {
      id: 'balance-1',
      warehouseId: 'wh-1',
      warehouseCode: 'WH-FG',
      warehouseName: 'Finished Goods',
      productId: 'p-1',
      productCode: 'P-001',
      productName: 'Storage Box',
      quantity: 12.5,
      updatedAt: '2026-01-02T00:00:00Z',
    };
  }

  function transactionPage(): ProductInventoryTransactionPage {
    return {
      items: [
        {
          id: 'tx-1',
          warehouseId: 'wh-1',
          warehouseCode: 'WH-FG',
          warehouseName: 'Finished Goods',
          productId: 'p-1',
          productCode: 'P-001',
          productName: 'Storage Box',
          type: 'ProductionOutput',
          quantity: 12.5,
          signedQuantity: 12.5,
          referenceType: 'ProductionOrder',
          referenceId: 'po-1',
          note: 'Production order output.',
          createdAt: '2026-01-02T00:00:00Z',
        },
      ],
      page: 1,
      pageSize: 25,
      totalCount: 30,
    };
  }

  function success<T>(data: T): Observable<ApiResponse<T>> {
    return of({ success: true, message: 'OK', data });
  }
});
