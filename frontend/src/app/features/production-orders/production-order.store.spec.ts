import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { BomApiService } from '../boms/bom-api.service';
import { InventoryApiService } from '../inventories/inventory-api.service';
import { MachineApiService } from '../machines/machine-api.service';
import { ProductApiService } from '../products/product-api.service';
import { ProductionOrderApiService } from './production-order-api.service';
import { ProductionOrder, ProductionOrderOperation } from './production-order.models';
import { ProductionOrderStore } from './production-order.store';

describe('ProductionOrderStore lifecycle', () => {
  let store: ProductionOrderStore;
  let api: jasmine.SpyObj<ProductionOrderApiService>;
  let machines: jasmine.SpyObj<MachineApiService>;

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
    const products = jasmine.createSpyObj<ProductApiService>('ProductApiService', ['getProducts']);
    const boms = jasmine.createSpyObj<BomApiService>('BomApiService', [
      'getProductionOrderRequirements',
    ]);
    const inventories = jasmine.createSpyObj<InventoryApiService>('InventoryApiService', [
      'getWarehouses',
    ]);
    machines = jasmine.createSpyObj<MachineApiService>('MachineApiService', ['getMachines']);
    api.getProductionOrders.and.returnValue(success([order()]));
    api.getOperations.and.returnValue(success([operation()]));
    machines.getMachines.and.returnValue(success([]));
    products.getProducts.and.returnValue(success([]));
    inventories.getWarehouses.and.returnValue(success([]));
    TestBed.configureTestingModule({
      providers: [
        ProductionOrderStore,
        { provide: ProductionOrderApiService, useValue: api },
        { provide: ProductApiService, useValue: products },
        { provide: BomApiService, useValue: boms },
        { provide: InventoryApiService, useValue: inventories },
        { provide: MachineApiService, useValue: machines },
      ],
    });
    store = TestBed.inject(ProductionOrderStore);
  });

  it('releases and reloads the canonical list', async () => {
    api.releaseProductionOrder.and.returnValue(success(order('released')));
    expect(await store.release('po-1')).toBeTrue();
    expect(api.getProductionOrders).toHaveBeenCalled();
    expect(store.orders()).toEqual([order()]);
  });

  it('preserves a controlled release error', async () => {
    api.releaseProductionOrder.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 409,
            error: { detail: 'The product does not have an active routing.' },
          }),
      ),
    );
    expect(await store.release('po-1')).toBeFalse();
    expect(store.error()).toBe('The product does not have an active routing.');
  });

  it('starts with allocations then refreshes orders, operations, and machines', async () => {
    const input = { allocations: [{ materialId: 'mat-1', warehouseId: 'wh-1', quantity: 5 }] };
    api.startProductionOrder.and.returnValue(success(order('in_progress')));
    expect(await store.start('po-1', input)).toBeTrue();
    expect(api.startProductionOrder).toHaveBeenCalledWith('po-1', input);
    expect(api.getProductionOrders).toHaveBeenCalled();
    expect(api.getOperations).toHaveBeenCalledWith('po-1');
    expect(machines.getMachines).toHaveBeenCalled();
  });

  it('keeps start errors for the allocation dialog', async () => {
    api.startProductionOrder.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 409,
            error: { detail: 'Insufficient stock.' },
          }),
      ),
    );
    expect(await store.start('po-1', { allocations: [] })).toBeFalse();
    expect(store.error()).toBe('Insufficient stock.');
  });

  it('completes and cancels through canonical mutations', async () => {
    api.completeProductionOrder.and.returnValue(success(order('completed')));
    api.cancelProductionOrder.and.returnValue(success(order('cancelled')));
    expect(await store.complete('po-1', { warehouseId: 'wh-fg' })).toBeTrue();
    expect(api.completeProductionOrder).toHaveBeenCalledWith('po-1', { warehouseId: 'wh-fg' });
    expect(await store.cancel('po-1')).toBeTrue();
    expect(api.cancelProductionOrder).toHaveBeenCalledWith('po-1');
  });

  it('loads operation state from the dedicated endpoint', async () => {
    expect(await store.loadOperations('po-1')).toBeTrue();
    expect(store.operations()).toEqual([operation()]);
  });

  function order(status: ProductionOrder['status'] = 'planned'): ProductionOrder {
    return {
      id: 'po-1',
      number: 'PO-001',
      productId: 'product-1',
      productCode: 'P-001',
      productName: 'Product',
      quantity: 5,
      status,
      billOfMaterialId: 'bom-1',
      bomRevision: 2,
      routingId: 'routing-1',
      routingRevision: 3,
      operations: [],
      releasedAt: null,
      startedAt: null,
      completedAt: null,
      cancelledAt: null,
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-01T00:00:00Z',
    };
  }

  function operation(): ProductionOrderOperation {
    return {
      id: 'op-1',
      productionOrderId: 'po-1',
      routingOperationId: 'route-op-1',
      sequence: 1,
      name: 'Cut',
      workCenterId: 'wc-1',
      workCenterCode: 'WC-1',
      workCenterName: 'Cutting',
      machineId: null,
      machineCode: null,
      machineName: null,
      setupTimeMinutes: 5,
      runTimeMinutes: 10,
      description: null,
      status: 'pending',
      startedAt: null,
      completedAt: null,
      createdAt: '2026-01-01T00:00:00Z',
    };
  }

  function success<T>(data: T): Observable<ApiResponse<T>> {
    return of({ success: true, message: 'OK', data });
  }
});
