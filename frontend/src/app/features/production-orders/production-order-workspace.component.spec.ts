import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, of, Subject, throwError } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { BomApiService } from '../boms/bom-api.service';
import { MaterialRequirements } from '../boms/bom.models';
import { InventoryApiService } from '../inventories/inventory-api.service';
import { MachineApiService } from '../machines/machine-api.service';
import { ProductApiService } from '../products/product-api.service';
import { ProductionOrderApiService } from './production-order-api.service';
import { ProductionOrder } from './production-order.models';
import { ProductionOrderWorkspaceComponent } from './production-order-workspace.component';

describe('ProductionOrderWorkspaceComponent', () => {
  let api: jasmine.SpyObj<ProductionOrderApiService>;
  let bomApi: jasmine.SpyObj<BomApiService>;

  beforeEach(() => {
    api = jasmine.createSpyObj<ProductionOrderApiService>('ProductionOrderApiService', [
      'getProductionOrders',
      'createProductionOrder',
      'updateProductionOrder',
      'deleteProductionOrder',
      'releaseProductionOrder',
      'startProductionOrder',
      'completeProductionOrder',
      'cancelProductionOrder',
      'getOperations',
      'startOperation',
      'completeOperation',
    ]);
    bomApi = jasmine.createSpyObj<BomApiService>('BomApiService', [
      'getProductionOrderRequirements',
    ]);
    const productApi = jasmine.createSpyObj<ProductApiService>('ProductApiService', [
      'getProducts',
    ]);
    const inventoryApi = jasmine.createSpyObj<InventoryApiService>('InventoryApiService', [
      'getWarehouses',
    ]);
    const machineApi = jasmine.createSpyObj<MachineApiService>('MachineApiService', [
      'getMachines',
    ]);
    productApi.getProducts.and.returnValue(
      success([
        {
          id: 'product-1',
          code: 'P-001',
          name: 'Product',
          createdAt: '2026-01-01T00:00:00Z',
          updatedAt: '2026-01-01T00:00:00Z',
        },
      ]),
    );
    inventoryApi.getWarehouses.and.returnValue(
      success([
        {
          id: 'wh-1',
          code: 'WH-1',
          name: 'Warehouse 1',
          description: null,
          isActive: true,
          createdAt: '',
          updatedAt: '',
        },
        {
          id: 'wh-2',
          code: 'WH-2',
          name: 'Warehouse 2',
          description: null,
          isActive: true,
          createdAt: '',
          updatedAt: '',
        },
      ]),
    );
    machineApi.getMachines.and.returnValue(success([]));
    api.getOperations.and.returnValue(success([]));
    bomApi.getProductionOrderRequirements.and.returnValue(success(requirements()));
    TestBed.configureTestingModule({
      providers: [
        { provide: ProductionOrderApiService, useValue: api },
        { provide: BomApiService, useValue: bomApi },
        { provide: ProductApiService, useValue: productApi },
        { provide: InventoryApiService, useValue: inventoryApi },
        { provide: MachineApiService, useValue: machineApi },
      ],
    });
  });

  it('shows only planned lifecycle mutations for a planned order', async () => {
    const fixture = await create('planned');
    expect(buttonTexts(fixture)).toContain('Phát hành lệnh');
    expect(buttonTexts(fixture)).toContain('Sửa');
    expect(buttonTexts(fixture)).not.toContain('Bắt đầu sản xuất');
    expect(buttonTexts(fixture)).not.toContain('Hoàn thành lệnh');
  });

  it('shows start but not edit or delete for a released order', async () => {
    const fixture = await create('released');
    expect(buttonTexts(fixture)).toContain('Bắt đầu sản xuất');
    expect(buttonTexts(fixture)).toContain('Công đoạn thực thi');
    expect(buttonTexts(fixture)).not.toContain('Sửa');
    expect(buttonTexts(fixture)).not.toContain('Xóa');
  });

  it('shows execution and completion for in-progress, with completed and cancelled read-only', async () => {
    let fixture = await create('in_progress');
    expect(buttonTexts(fixture)).toContain('Công đoạn thực thi');
    expect(buttonTexts(fixture)).toContain('Hoàn thành lệnh');
    fixture.destroy();

    api.getProductionOrders.and.returnValue(success(orderPage([order('completed')])));
    fixture = TestBed.createComponent(ProductionOrderWorkspaceComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(buttonTexts(fixture)).toContain('Công đoạn thực thi');
    expect(buttonTexts(fixture)).not.toContain('Hoàn thành lệnh');
    fixture.destroy();

    api.getProductionOrders.and.returnValue(success(orderPage([order('cancelled')])));
    fixture = TestBed.createComponent(ProductionOrderWorkspaceComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(buttonTexts(fixture)).not.toContain('Công đoạn thực thi');
    expect(fixture.nativeElement.textContent).toContain('Chỉ đọc');
  });

  it('supports split allocations and submits the exact allocation structure', async () => {
    const fixture = await create('released');
    api.startProductionOrder.and.returnValue(success(order('in_progress')));
    clickButton(fixture, 'Bắt đầu sản xuất');
    await fixture.whenStable();
    fixture.detectChanges();
    clickButton(fixture, 'Thêm kho cấp phát');
    fixture.detectChanges();
    const rows = Array.from(
      fixture.nativeElement.querySelectorAll('.allocation-row'),
    ) as HTMLElement[];
    expect(rows.length).toBe(2);
    setSelect(rows[0], 'wh-1');
    setInput(rows[0], '3');
    setSelect(rows[1], 'wh-2');
    setInput(rows[1], '2');
    fixture.detectChanges();
    clickButton(fixture, 'Tiếp tục');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Bắt đầu PO-001?');
    clickButton(fixture, 'Bắt đầu sản xuất');
    await fixture.whenStable();
    expect(api.startProductionOrder).toHaveBeenCalledWith('po-1', {
      allocations: [
        { materialId: 'mat-1', warehouseId: 'wh-1', quantity: 3 },
        { materialId: 'mat-1', warehouseId: 'wh-2', quantity: 2 },
      ],
    });
  });

  it('blocks mismatched allocation totals before confirmation', async () => {
    const fixture = await create('released');
    clickButton(fixture, 'Bắt đầu sản xuất');
    await fixture.whenStable();
    fixture.detectChanges();
    const row = fixture.nativeElement.querySelector('.allocation-row') as HTMLElement;
    setSelect(row, 'wh-1');
    setInput(row, '4');
    fixture.detectChanges();
    clickButton(fixture, 'Tiếp tục');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Bắt đầu PO-001?');
    expect(fixture.nativeElement.textContent).toContain('phân bổ đúng tổng nhu cầu');
    expect(api.startProductionOrder).not.toHaveBeenCalled();
  });

  it('keeps allocation state open when backend start validation fails', async () => {
    const fixture = await create('released');
    api.startProductionOrder.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 409,
            error: { detail: 'Insufficient stock in the selected warehouse.' },
          }),
      ),
    );
    clickButton(fixture, 'Bắt đầu sản xuất');
    await fixture.whenStable();
    fixture.detectChanges();
    setSelect(fixture.nativeElement.querySelector('.allocation-row') as HTMLElement, 'wh-1');
    fixture.detectChanges();
    clickButton(fixture, 'Tiếp tục');
    fixture.detectChanges();
    clickButton(fixture, 'Bắt đầu sản xuất');
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.start-panel')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Insufficient stock');
    clickButton(fixture, 'Quay lại');
    fixture.detectChanges();
    const retainedWarehouse = fixture.nativeElement.querySelector(
      '.allocation-row select',
    ) as HTMLSelectElement;
    expect(retainedWarehouse.value).toBe('wh-1');
  });

  it('requires a destination warehouse before completing', async () => {
    const fixture = await create('in_progress');
    api.completeProductionOrder.and.returnValue(success(order('completed')));
    clickButton(fixture, 'Hoàn thành lệnh');
    fixture.detectChanges();
    clickButton(fixture, 'Hoàn thành lệnh');
    fixture.detectChanges();
    expect(api.completeProductionOrder).not.toHaveBeenCalled();
    const select = fixture.nativeElement.querySelector(
      '.confirmation-panel select',
    ) as HTMLSelectElement;
    select.value = 'wh-2';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    clickButton(fixture, 'Hoàn thành lệnh');
    await fixture.whenStable();
    expect(api.completeProductionOrder).toHaveBeenCalledWith('po-1', { warehouseId: 'wh-2' });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.confirmation-panel')).toBeNull();
  });

  it('requires explicit cancel confirmation', async () => {
    const fixture = await create('planned');
    api.cancelProductionOrder.and.returnValue(success(order('cancelled')));
    clickButton(fixture, 'Hủy lệnh');
    fixture.detectChanges();
    expect(api.cancelProductionOrder).not.toHaveBeenCalled();
    clickButton(fixture, 'Xác nhận hủy');
    await fixture.whenStable();
    expect(api.cancelProductionOrder).toHaveBeenCalledWith('po-1');
  });

  it('disables cancel confirmation while the request is saving', async () => {
    const fixture = await create('planned');
    const pending = new Subject<ApiResponse<ProductionOrder>>();
    api.cancelProductionOrder.and.returnValue(pending.asObservable());
    clickButton(fixture, 'Hủy lệnh');
    fixture.detectChanges();
    clickButton(fixture, 'Xác nhận hủy');
    fixture.detectChanges();
    const confirm = (
      Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[]
    ).find((item) => item.textContent?.trim() === 'Đang hủy...');
    expect(confirm?.disabled).toBeTrue();
    pending.next({ success: true, message: 'OK', data: order('cancelled') });
    pending.complete();
    await fixture.whenStable();
  });

  it('renders deterministic planning facts with text labels', async () => {
    const fixture = await create('planned');
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Khẩn cấp');
    expect(text).toContain('Còn 1 ngày');
    expect(text).toContain('09/09/2026');
    expect(text).toContain('không phải dự báo ETA');
  });

  it('uses quick risk filters against the planning endpoint', async () => {
    const fixture = await create('planned');
    api.getProductionOrders.calls.reset();
    clickButton(fixture, 'Quá hạn');
    await fixture.whenStable();

    expect(api.getProductionOrders).toHaveBeenCalledWith(
      jasmine.objectContaining({
        deliveryStatus: 'overdue',
        sortBy: 'dueDate',
        sortDirection: 'asc',
        page: 1,
      }),
      true,
    );
  });

  it('normalizes a business due date to end-of-day UTC when saving', async () => {
    const fixture = await create('planned');
    api.updateProductionOrder.and.returnValue(success(order('planned')));
    clickButton(fixture, 'Sửa');
    fixture.detectChanges();
    const dueDate = fixture.nativeElement.querySelector(
      'input[formcontrolname="dueDate"]',
    ) as HTMLInputElement;
    dueDate.value = '2026-09-12';
    dueDate.dispatchEvent(new Event('input'));
    const priority = fixture.nativeElement.querySelector(
      '.editor select[formcontrolname="priority"]',
    ) as HTMLSelectElement;
    priority.value = 'high';
    priority.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    clickButton(fixture, 'Lưu lệnh sản xuất');
    await fixture.whenStable();

    expect(api.updateProductionOrder).toHaveBeenCalledWith(
      'po-1',
      jasmine.objectContaining({
        dueDate: '2026-09-12T23:59:59.999Z',
        priority: 'high',
      }),
    );
  });

  async function create(
    status: ProductionOrder['status'],
  ): Promise<ComponentFixture<ProductionOrderWorkspaceComponent>> {
    api.getProductionOrders.and.returnValue(success(orderPage([order(status)])));
    const fixture = TestBed.createComponent(ProductionOrderWorkspaceComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  function clickButton(
    fixture: ComponentFixture<ProductionOrderWorkspaceComponent>,
    text: string,
  ): void {
    const button = (
      Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[]
    )
      .filter((item) => item.textContent?.trim() === text)
      .pop();
    expect(button).withContext(`Expected button ${text}`).toBeDefined();
    button?.click();
  }

  function setSelect(row: HTMLElement, value: string): void {
    const select = row.querySelector('select') as HTMLSelectElement;
    select.value = value;
    select.dispatchEvent(new Event('change'));
  }

  function setInput(row: HTMLElement, value: string): void {
    const input = row.querySelector('input') as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function buttonTexts(fixture: ComponentFixture<ProductionOrderWorkspaceComponent>): string[] {
    return (
      Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[]
    ).map((button) => button.textContent?.trim() ?? '');
  }

  function requirements(): MaterialRequirements {
    return {
      productId: 'product-1',
      productCode: 'P-001',
      productName: 'Product',
      bomId: 'bom-1',
      bomRevision: 2,
      requestedQuantity: 5,
      canProduce: true,
      materials: [
        {
          materialId: 'mat-1',
          materialCode: 'MAT-1',
          materialName: 'Steel',
          unit: 'kg',
          quantityPerBom: 1,
          scrapPercentage: null,
          requiredQuantity: 5,
          availableQuantity: 10,
          shortageQuantity: 0,
          isSufficient: true,
        },
      ],
    };
  }

  function order(status: ProductionOrder['status']): ProductionOrder {
    return {
      id: 'po-1',
      number: 'PO-001',
      productId: 'product-1',
      productCode: 'P-001',
      productName: 'Product',
      quantity: 5,
      status,
      dueDate: '2026-09-09T23:59:59.999Z',
      priority: 'urgent',
      deliveryStatus: 'due_soon',
      daysUntilDue: 1,
      isOverdue: false,
      isDueSoon: true,
      isCompletedLate: false,
      billOfMaterialId: status === 'planned' ? null : 'bom-1',
      bomRevision: status === 'planned' ? null : 2,
      routingId: status === 'planned' ? null : 'routing-1',
      routingRevision: status === 'planned' ? null : 3,
      operations: [],
      releasedAt: null,
      startedAt: null,
      completedAt: null,
      cancelledAt: null,
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-01T00:00:00Z',
    };
  }

  function success<T>(data: T): Observable<ApiResponse<T>> {
    return of({ success: true, message: 'OK', data });
  }

  function orderPage(items: ProductionOrder[]) {
    return { items, page: 1, pageSize: 50, totalCount: items.length };
  }
});
