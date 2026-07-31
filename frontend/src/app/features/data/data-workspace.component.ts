import { Component, signal } from '@angular/core';
import { MachineWorkspaceComponent } from '../machines/machine-workspace.component';
import { InventoryWorkspaceComponent } from '../inventories/inventory-workspace.component';
import { MaterialWorkspaceComponent } from '../materials/material-workspace.component';
import { ProductWorkspaceComponent } from '../products/product-workspace.component';
import { ProductionOrderWorkspaceComponent } from '../production-orders/production-order-workspace.component';

type DataView = 'machines' | 'materials' | 'inventories' | 'products' | 'production-orders';

@Component({
  selector: 'app-data-workspace',
  imports: [
    MachineWorkspaceComponent,
    MaterialWorkspaceComponent,
    InventoryWorkspaceComponent,
    ProductWorkspaceComponent,
    ProductionOrderWorkspaceComponent,
  ],
  templateUrl: './data-workspace.component.html',
  styleUrl: './data-workspace.component.scss',
})
export class DataWorkspaceComponent {
  protected readonly activeView = signal<DataView>('machines');

  protected selectView(view: DataView): void {
    this.activeView.set(view);
  }
}
