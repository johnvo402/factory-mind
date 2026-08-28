import { TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import { ApiResponse } from '../../core/api/api.models';
import { MaterialApiService } from '../materials/material-api.service';
import { Material } from '../materials/material.models';
import { BomApiService } from './bom-api.service';
import { Bom, MaterialRequirements } from './bom.models';
import { BomStore } from './bom.store';

describe('BomStore', () => {
  let store: BomStore;
  let api: jasmine.SpyObj<BomApiService>;
  let materialApi: jasmine.SpyObj<MaterialApiService>;

  beforeEach(() => {
    api = jasmine.createSpyObj<BomApiService>('BomApiService', [
      'getBoms',
      'createBom',
      'updateBom',
      'activateBom',
      'archiveBom',
      'getProductRequirements',
    ]);
    materialApi = jasmine.createSpyObj<MaterialApiService>('MaterialApiService', ['getMaterials']);
    TestBed.configureTestingModule({
      providers: [
        BomStore,
        { provide: BomApiService, useValue: api },
        { provide: MaterialApiService, useValue: materialApi },
      ],
    });
    store = TestBed.inject(BomStore);
  });

  it('loads product revisions together with material options', async () => {
    api.getBoms.and.returnValue(success([bom()]));
    materialApi.getMaterials.and.returnValue(success([material()]));

    await store.load('product-1');

    expect(api.getBoms).toHaveBeenCalledWith('product-1');
    expect(store.boms()[0].status).toBe('active');
    expect(store.materials()).toEqual([material()]);
  });

  it('calculates and stores the product requirement preview', async () => {
    api.getProductRequirements.and.returnValue(success(requirements()));

    await store.calculate('product-1', 100);

    expect(api.getProductRequirements).toHaveBeenCalledWith('product-1', 100);
    expect(store.requirements()?.canProduce).toBeFalse();
    expect(store.requirements()?.materials[0].shortageQuantity).toBe(10);
  });

  function bom(): Bom {
    return {
      id: 'bom-1',
      productId: 'product-1',
      productCode: 'P001',
      productName: 'Table',
      revision: 1,
      outputQuantity: 1,
      status: 'active',
      items: [],
      createdAt: '2026-08-28T00:00:00Z',
      updatedAt: '2026-08-28T00:00:00Z',
    };
  }

  function material(): Material {
    return {
      id: 'material-1',
      code: 'STEEL',
      name: 'Steel',
      unit: 'kg',
      createdAt: '2026-08-28T00:00:00Z',
      updatedAt: '2026-08-28T00:00:00Z',
    };
  }

  function requirements(): MaterialRequirements {
    return {
      productId: 'product-1',
      productCode: 'P001',
      productName: 'Table',
      bomId: 'bom-1',
      bomRevision: 1,
      requestedQuantity: 100,
      canProduce: false,
      materials: [
        {
          materialId: 'material-1',
          materialCode: 'STEEL',
          materialName: 'Steel',
          unit: 'kg',
          quantityPerBom: 0.5,
          scrapPercentage: null,
          requiredQuantity: 50,
          availableQuantity: 40,
          shortageQuantity: 10,
          isSufficient: false,
        },
      ],
    };
  }

  function success<T>(data: T): Observable<ApiResponse<T>> {
    return of({ success: true, message: 'OK', data });
  }
});
