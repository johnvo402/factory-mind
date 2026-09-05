import { Component, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ExcelImportWizardComponent } from '../excel-imports/excel-import-wizard.component';
import { ExcelImportEntityType } from '../excel-imports/excel-import.models';
import { InventoryStore } from '../inventories/inventory.store';
import { MachineWorkspaceComponent } from '../machines/machine-workspace.component';
import { InventoryWorkspaceComponent } from '../inventories/inventory-workspace.component';
import { MaterialWorkspaceComponent } from '../materials/material-workspace.component';
import { MaterialStore } from '../materials/material.store';
import { MachineStore } from '../machines/machine.store';
import { ProductWorkspaceComponent } from '../products/product-workspace.component';
import { ProductStore } from '../products/product.store';
import { ProductionOrderWorkspaceComponent } from '../production-orders/production-order-workspace.component';
import { ProductionOrderStore } from '../production-orders/production-order.store';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { WorkCenterWorkspaceComponent } from '../work-centers/work-center-workspace.component';

type DataView = 'machines' | 'work-centers' | 'materials' | 'inventories' | 'products' | 'production-orders';
const DATA_VIEWS: readonly DataView[] = ['machines', 'work-centers', 'materials', 'inventories', 'products', 'production-orders'];

@Component({
  selector: 'app-data-workspace',
  imports: [
    MachineWorkspaceComponent,
    MaterialWorkspaceComponent,
    InventoryWorkspaceComponent,
    ProductWorkspaceComponent,
    ProductionOrderWorkspaceComponent,
    ExcelImportWizardComponent,
    RouterLink,
    UiIconComponent,
    WorkCenterWorkspaceComponent,
  ],
  templateUrl: './data-workspace.component.html',
  styleUrl: './data-workspace.component.scss',
})
export class DataWorkspaceComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly machines = inject(MachineStore);
  private readonly materials = inject(MaterialStore);
  private readonly inventories = inject(InventoryStore);
  private readonly products = inject(ProductStore);
  private readonly orders = inject(ProductionOrderStore);
  private readonly routeParams = toSignal(this.route.paramMap, { initialValue: this.route.snapshot.paramMap });
  protected readonly activeView = computed<DataView>(() => {
    const view = this.routeParams().get('view') as DataView | null;
    return view && DATA_VIEWS.includes(view) ? view : 'machines';
  });
  protected readonly importOpen = signal(false);
  protected readonly importMessage = signal('');

  constructor() {
    effect(() => {
      this.activeView();
      this.importMessage.set('');
    });
  }

  protected importEntityType(): ExcelImportEntityType {
    switch (this.activeView()) {
      case 'materials': return 'material';
      case 'inventories': return 'inventory';
      case 'products': return 'product';
      case 'production-orders': return 'production_order';
      default: return 'machine';
    }
  }

  protected async handleImported(count: number): Promise<void> {
    this.importMessage.set(`Đã import ${count} dòng thành công.`);
    this.importOpen.set(false);
    switch (this.activeView()) {
      case 'materials': await this.materials.load(); break;
      case 'inventories': await this.inventories.initialize(); break;
      case 'products': await this.products.load(); break;
      case 'production-orders': await this.orders.initialize(); break;
      default: await this.machines.load();
    }
  }
}
