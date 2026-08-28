import { TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { BomApiService } from '../boms/bom-api.service';
import { InventoryApiService } from '../inventories/inventory-api.service';
import { Inventory } from '../inventories/inventory.models';
import { InventoryStore } from '../inventories/inventory.store';
import { MaterialApiService } from '../materials/material-api.service';
import { Material } from '../materials/material.models';
import { MaterialStore } from '../materials/material.store';
import { ProductApiService } from '../products/product-api.service';
import { Product } from '../products/product.models';
import { ProductStore } from '../products/product.store';
import { ProductionOrderApiService } from '../production-orders/production-order-api.service';
import { ProductionOrder } from '../production-orders/production-order.models';
import { ProductionOrderStore } from '../production-orders/production-order.store';

describe('Business data stores', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('loads materials with a trimmed search', async () => {
    const api = jasmine.createSpyObj<MaterialApiService>('MaterialApiService', [
      'getMaterials',
      'createMaterial',
      'updateMaterial',
      'deleteMaterial',
    ]);
    api.getMaterials.and.returnValue(success([material()]));
    TestBed.configureTestingModule({
      providers: [MaterialStore, { provide: MaterialApiService, useValue: api }],
    });
    const store = TestBed.inject(MaterialStore);

    await store.load('  polypropylene  ');

    expect(api.getMaterials).toHaveBeenCalledWith('polypropylene');
    expect(store.materials()).toEqual([material()]);
  });

  it('creates a product and reloads the canonical list', async () => {
    const api = jasmine.createSpyObj<ProductApiService>('ProductApiService', [
      'getProducts',
      'createProduct',
      'updateProduct',
      'deleteProduct',
    ]);
    api.createProduct.and.returnValue(success(product()));
    api.getProducts.and.returnValue(success([product()]));
    TestBed.configureTestingModule({
      providers: [ProductStore, { provide: ProductApiService, useValue: api }],
    });
    const store = TestBed.inject(ProductStore);

    const saved = await store.save(null, { code: 'PRD-001', name: 'Storage Box' });

    expect(saved).toBeTrue();
    expect(store.products()).toEqual([product()]);
  });

  it('initializes inventory together with material options', async () => {
    const inventoryApi = jasmine.createSpyObj<InventoryApiService>('InventoryApiService', [
      'getInventories',
      'getWarehouses',
      'receive',
      'issue',
      'adjust',
      'transfer',
    ]);
    const materialApi = jasmine.createSpyObj<MaterialApiService>('MaterialApiService', [
      'getMaterials',
    ]);
    inventoryApi.getInventories.and.returnValue(success([inventory()]));
    inventoryApi.getWarehouses.and.returnValue(success([warehouse()]));
    materialApi.getMaterials.and.returnValue(success([material()]));
    TestBed.configureTestingModule({
      providers: [
        InventoryStore,
        { provide: InventoryApiService, useValue: inventoryApi },
        { provide: MaterialApiService, useValue: materialApi },
      ],
    });
    const store = TestBed.inject(InventoryStore);

    await store.initialize();

    expect(store.inventories()).toEqual([inventory()]);
    expect(store.materials()).toEqual([material()]);
    expect(store.warehouses()).toEqual([warehouse()]);
  });

  it('initializes production orders together with product options', async () => {
    const orderApi = jasmine.createSpyObj<ProductionOrderApiService>(
      'ProductionOrderApiService',
      ['getProductionOrders', 'createProductionOrder', 'updateProductionOrder', 'deleteProductionOrder'],
    );
    const productApi = jasmine.createSpyObj<ProductApiService>('ProductApiService', ['getProducts']);
    const bomApi = jasmine.createSpyObj<BomApiService>('BomApiService', [
      'getProductionOrderRequirements',
    ]);
    orderApi.getProductionOrders.and.returnValue(success([productionOrder()]));
    productApi.getProducts.and.returnValue(success([product()]));
    TestBed.configureTestingModule({
      providers: [
        ProductionOrderStore,
        { provide: ProductionOrderApiService, useValue: orderApi },
        { provide: ProductApiService, useValue: productApi },
        { provide: BomApiService, useValue: bomApi },
      ],
    });
    const store = TestBed.inject(ProductionOrderStore);

    await store.initialize();

    expect(store.orders()).toEqual([productionOrder()]);
    expect(store.products()).toEqual([product()]);
  });

  function material(): Material {
    return {
      id: 'material-1',
      code: 'MAT-PP',
      name: 'Polypropylene Resin',
      unit: 'kg',
      createdAt: '2026-08-01T00:00:00Z',
      updatedAt: '2026-08-01T00:00:00Z',
    };
  }

  function product(): Product {
    return {
      id: 'product-1',
      code: 'PRD-001',
      name: 'Storage Box',
      createdAt: '2026-08-01T00:00:00Z',
      updatedAt: '2026-08-01T00:00:00Z',
    };
  }

  function inventory(): Inventory {
    return {
      id: 'inventory-1',
      warehouseId: 'warehouse-1',
      warehouseCode: 'WH-RAW',
      warehouseName: 'Raw Materials',
      materialId: 'material-1',
      materialCode: 'MAT-PP',
      materialName: 'Polypropylene Resin',
      unit: 'kg',
      quantity: 1200,
      updatedAt: '2026-08-01T00:00:00Z',
    };
  }

  function warehouse() {
    return {
      id: 'warehouse-1',
      code: 'WH-RAW',
      name: 'Raw Materials',
      description: null,
      isActive: true,
      createdAt: '2026-08-01T00:00:00Z',
      updatedAt: '2026-08-01T00:00:00Z',
    };
  }

  function productionOrder(): ProductionOrder {
    return {
      id: 'order-1',
      number: 'PO-001',
      productId: 'product-1',
      productCode: 'PRD-001',
      productName: 'Storage Box',
      quantity: 500,
      status: 'planned',
      createdAt: '2026-08-01T00:00:00Z',
      updatedAt: '2026-08-01T00:00:00Z',
    };
  }

  function success<T>(data: T): Observable<ApiResponse<T>> {
    return of({ success: true, message: 'OK', data });
  }
});
