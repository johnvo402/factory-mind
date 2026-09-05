import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { DialogFocusDirective } from '../../shared/ui/dialog-focus.directive';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import {
  Inventory,
  InventoryAdjustmentInput,
  InventoryMovementInput,
  InventoryTransactionType,
  InventoryTransferInput,
  Warehouse,
  WarehouseUpdateInput,
} from './inventory.models';
import { InventoryStore } from './inventory.store';

type InventoryOperation = 'receive' | 'issue' | 'adjust' | 'transfer';

@Component({
  selector: 'app-inventory-workspace',
  imports: [DatePipe, DecimalPipe, ReactiveFormsModule, DialogFocusDirective, UiIconComponent],
  templateUrl: './inventory-workspace.component.html',
  styleUrls: ['../data/entity-workspace.scss', './inventory-workspace.component.scss'],
})
export class InventoryWorkspaceComponent implements OnInit {
  protected readonly store = inject(InventoryStore);
  protected readonly operation = signal<InventoryOperation | null>(null);
  protected readonly historyOpen = signal(false);
  protected readonly warehouseEditorOpen = signal(false);
  protected readonly editingWarehouseId = signal<string | null>(null);
  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly operationForm = new FormGroup({
    sourceWarehouseId: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    destinationWarehouseId: new FormControl('', { nonNullable: true }),
    materialId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    quantity: new FormControl(1, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0.001)],
    }),
    direction: new FormControl<'Increase' | 'Decrease'>('Increase', { nonNullable: true }),
    note: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] }),
    referenceType: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(100)],
    }),
  });
  protected readonly warehouseForm = new FormGroup({
    code: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(50)],
    }),
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(500)],
    }),
    isActive: new FormControl(true, { nonNullable: true }),
  });

  ngOnInit(): void {
    void this.store.initialize();
  }

  protected activeWarehouses(): Warehouse[] {
    return this.store.warehouses().filter((warehouse) => warehouse.isActive);
  }

  protected search(event: SubmitEvent): void {
    event.preventDefault();
    void this.store.load(this.searchControl.getRawValue());
  }

  protected clearSearch(): void {
    this.searchControl.setValue('');
    void this.store.load('');
  }

  protected startOperation(operation: InventoryOperation, inventory?: Inventory): void {
    this.store.clearError();
    const sourceWarehouseId = inventory?.warehouseId ?? this.activeWarehouses()[0]?.id ?? '';
    const destination = this.activeWarehouses().find(
      (warehouse) => warehouse.id !== sourceWarehouseId,
    );
    this.operationForm.reset({
      sourceWarehouseId,
      destinationWarehouseId: destination?.id ?? '',
      materialId: inventory?.materialId ?? this.store.materials()[0]?.id ?? '',
      quantity: 1,
      direction: 'Increase',
      note: '',
      referenceType: '',
    });
    this.operationForm.controls.note.setValidators([
      ...(operation === 'adjust' ? [Validators.required] : []),
      Validators.maxLength(500),
    ]);
    this.operationForm.controls.note.updateValueAndValidity();
    this.operation.set(operation);
  }

  protected cancelOperation(): void {
    this.operation.set(null);
  }

  protected operationTitle(): string {
    return {
      receive: 'Nhập kho',
      issue: 'Xuất kho',
      adjust: 'Điều chỉnh tồn kho',
      transfer: 'Chuyển kho',
    }[this.operation() ?? 'receive'];
  }

  protected async submitOperation(): Promise<void> {
    const operation = this.operation();
    if (!operation) return;
    if (operation === 'transfer' && !this.operationForm.controls.destinationWarehouseId.value) {
      this.operationForm.controls.destinationWarehouseId.setErrors({ required: true });
    }
    if (this.operationForm.invalid) {
      this.operationForm.markAllAsTouched();
      return;
    }
    const value = this.operationForm.getRawValue();
    const movement: InventoryMovementInput = {
      warehouseId: value.sourceWarehouseId,
      materialId: value.materialId,
      quantity: value.quantity,
      note: value.note.trim() || null,
      referenceType: value.referenceType.trim() || null,
      referenceId: null,
    };
    let saved = false;
    if (operation === 'receive') saved = await this.store.receive(movement);
    if (operation === 'issue') saved = await this.store.issue(movement);
    if (operation === 'adjust') {
      const adjustment: InventoryAdjustmentInput = { ...movement, direction: value.direction };
      saved = await this.store.adjust(adjustment);
    }
    if (operation === 'transfer') {
      const transfer: InventoryTransferInput = {
        sourceWarehouseId: value.sourceWarehouseId,
        destinationWarehouseId: value.destinationWarehouseId,
        materialId: value.materialId,
        quantity: value.quantity,
        note: value.note.trim() || null,
        referenceType: value.referenceType.trim() || null,
      };
      saved = await this.store.transfer(transfer);
    }
    if (saved) this.cancelOperation();
  }

  protected async showHistory(): Promise<void> {
    if (await this.store.loadHistory()) this.historyOpen.set(true);
  }

  protected operationLabel(type: InventoryTransactionType): string {
    return {
      Receipt: 'Nhập kho',
      Issue: 'Xuất kho',
      AdjustmentIncrease: 'Điều chỉnh tăng',
      AdjustmentDecrease: 'Điều chỉnh giảm',
      TransferIn: 'Chuyển vào',
      TransferOut: 'Chuyển ra',
      ProductionConsume: 'Sản xuất tiêu thụ',
      ProductionOutput: 'Sản xuất hoàn thành',
    }[type];
  }

  protected openWarehouseEditor(): void {
    this.editingWarehouseId.set(null);
    this.warehouseForm.reset({ code: '', name: '', description: '', isActive: true });
    this.warehouseEditorOpen.set(true);
  }

  protected editWarehouse(warehouse: Warehouse): void {
    this.editingWarehouseId.set(warehouse.id);
    this.warehouseForm.reset({
      code: warehouse.code,
      name: warehouse.name,
      description: warehouse.description ?? '',
      isActive: warehouse.isActive,
    });
  }

  protected newWarehouse(): void {
    this.editingWarehouseId.set(null);
    this.warehouseForm.reset({ code: '', name: '', description: '', isActive: true });
  }

  protected async saveWarehouse(): Promise<void> {
    if (this.warehouseForm.invalid) {
      this.warehouseForm.markAllAsTouched();
      return;
    }
    const value = this.warehouseForm.getRawValue();
    const input: WarehouseUpdateInput = {
      code: value.code,
      name: value.name,
      description: value.description.trim() || null,
      isActive: value.isActive,
    };
    if (await this.store.saveWarehouse(this.editingWarehouseId(), input)) {
      this.newWarehouse();
    }
  }
}
