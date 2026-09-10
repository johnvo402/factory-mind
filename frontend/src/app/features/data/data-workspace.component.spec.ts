import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BehaviorSubject } from 'rxjs';
import { ActivatedRoute, convertToParamMap, ParamMap, Router } from '@angular/router';
import { InventoryStore } from '../inventories/inventory.store';
import { MachineStore } from '../machines/machine.store';
import { MaterialStore } from '../materials/material.store';
import { ProductStore } from '../products/product.store';
import { ProductionOrderStore } from '../production-orders/production-order.store';
import { DataWorkspaceComponent } from './data-workspace.component';

describe('DataWorkspaceComponent import eligibility', () => {
  let fixture: ComponentFixture<DataWorkspaceComponent>;
  let routeParams: BehaviorSubject<ParamMap>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    routeParams = new BehaviorSubject(convertToParamMap({ view: 'machines' }));
    router = jasmine.createSpyObj<Router>('Router', ['navigateByUrl']);
    const route = {
      paramMap: routeParams.asObservable(),
      snapshot: { paramMap: routeParams.value },
    };
    await TestBed.configureTestingModule({
      imports: [DataWorkspaceComponent],
      providers: [
        { provide: ActivatedRoute, useValue: route },
        { provide: Router, useValue: router },
        { provide: MachineStore, useValue: { load: jasmine.createSpy('load') } },
        { provide: MaterialStore, useValue: { load: jasmine.createSpy('load') } },
        { provide: InventoryStore, useValue: { initialize: jasmine.createSpy('initialize') } },
        { provide: ProductStore, useValue: { load: jasmine.createSpy('load') } },
        { provide: ProductionOrderStore, useValue: { initialize: jasmine.createSpy('initialize') } },
      ],
    })
      .overrideComponent(DataWorkspaceComponent, {
        set: {
          template: `
            @if (importEntityType(); as entityType) {
              <button type="button" (click)="importOpen.set(true)">Nhập từ Excel</button>
              <span data-testid="entity-type">{{ entityType }}</span>
            }
            @if (importOpen() && importEntityType(); as openEntityType) {
              <span data-testid="open-entity-type">{{ openEntityType }}</span>
            }
          `,
        },
      })
      .compileComponents();
    fixture = TestBed.createComponent(DataWorkspaceComponent);
    fixture.detectChanges();
  });

  [
    ['machines', 'machine'],
    ['materials', 'material'],
    ['inventories', 'inventory'],
    ['products', 'product'],
    ['production-orders', 'production_order'],
  ].forEach(([view, entityType]) => {
    it(`shows import for ${view} and maps it to ${entityType}`, () => {
      navigateTo(view);

      expect(importButton()).not.toBeNull();
      expect(fixture.nativeElement.querySelector('[data-testid="entity-type"]').textContent).toBe(
        entityType,
      );
    });
  });

  ['work-centers', 'product-inventories'].forEach((view) => {
    it(`hides import for unsupported ${view}`, () => {
      navigateTo(view);

      expect(importButton()).toBeNull();
      expect(fixture.nativeElement.textContent).not.toContain('machine');
    });
  });

  it('closes an open wizard when navigation changes to an unsupported view', () => {
    importButton()?.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="open-entity-type"]')).not.toBeNull();

    navigateTo('work-centers');

    expect(fixture.nativeElement.querySelector('[data-testid="open-entity-type"]')).toBeNull();
  });

  it('sends an unknown data view to the not-found page', () => {
    navigateTo('unknown-view');

    expect(router.navigateByUrl).toHaveBeenCalledWith('/not-found');
  });

  function navigateTo(view: string): void {
    routeParams.next(convertToParamMap({ view }));
    fixture.detectChanges();
    TestBed.flushEffects();
    fixture.detectChanges();
  }

  function importButton(): HTMLButtonElement | null {
    return fixture.nativeElement.querySelector('button');
  }
});
