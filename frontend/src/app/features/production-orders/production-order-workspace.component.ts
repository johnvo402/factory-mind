import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Warehouse } from '../inventories/inventory.models';
import { DialogFocusDirective } from '../../shared/ui/dialog-focus.directive';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import {
  ProductionMaterialAllocationInput,
  ProductionOrder,
  ProductionOrderInput,
  ProductionOrderOperation,
  ProductionOrderStatus,
} from './production-order.models';
import { ProductionOrderStore } from './production-order.store';

interface AllocationDraft {
  warehouseId: string;
  quantity: number;
}

@Component({
  selector: 'app-production-order-workspace',
  imports: [DatePipe, DecimalPipe, ReactiveFormsModule, DialogFocusDirective, UiIconComponent],
  templateUrl: './production-order-workspace.component.html',
  styleUrls: ['../data/entity-workspace.scss', './production-order-workspace.component.scss'],
})
export class ProductionOrderWorkspaceComponent implements OnInit {
  protected readonly store = inject(ProductionOrderStore);
  protected readonly editingId = signal<string | null>(null);
  protected readonly editorOpen = signal(false);
  protected readonly confirmDeleteId = signal<string | null>(null);
  protected readonly requirementsOpen = signal(false);
  protected readonly requirementOrder = signal<ProductionOrder | null>(null);
  protected readonly releaseConfirmOrder = signal<ProductionOrder | null>(null);
  protected readonly startOrder = signal<ProductionOrder | null>(null);
  protected readonly startConfirmationOpen = signal(false);
  protected readonly startSubmitted = signal(false);
  protected readonly allocationDrafts = signal<Record<string, AllocationDraft[]>>({});
  protected readonly cancelConfirmOrder = signal<ProductionOrder | null>(null);
  protected readonly completeOrder = signal<ProductionOrder | null>(null);
  protected readonly executionOrderId = signal<string | null>(null);
  protected readonly selectedMachineIds = signal<Record<string, string>>({});
  protected readonly executionOrder = computed(
    () => this.store.orders().find((order) => order.id === this.executionOrderId()) ?? null,
  );
  protected readonly activeWarehouses = computed(() =>
    this.store.warehouses().filter((warehouse) => warehouse.isActive),
  );
  protected readonly allExecutionOperationsComplete = computed(
    () =>
      this.store.operations().length > 0 &&
      this.store.operations().every((operation) => operation.status === 'completed'),
  );
  protected readonly startReady = computed(() => {
    const requirements = this.store.requirements();
    if (!requirements?.materials.length) return false;
    return requirements.materials.every((material) => {
      const allocations = this.allocationDrafts()[material.materialId] ?? [];
      return (
        allocations.length > 0 &&
        allocations.every((allocation) => allocation.warehouseId && allocation.quantity > 0) &&
        this.roundQuantity(this.allocationTotal(material.materialId)) ===
          this.roundQuantity(material.requiredQuantity)
      );
    });
  });
  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly orderForm = new FormGroup({
    number: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(50)],
    }),
    productId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    quantity: new FormControl(1, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0.001)],
    }),
  });
  protected readonly completeForm = new FormGroup({
    warehouseId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  ngOnInit(): void {
    void this.store.initialize();
  }

  protected search(event: SubmitEvent): void {
    event.preventDefault();
    void this.store.load(this.searchControl.getRawValue());
  }

  protected clearSearch(): void {
    this.searchControl.setValue('');
    void this.store.load('');
  }

  protected startCreate(): void {
    this.store.clearError();
    this.editingId.set(null);
    this.orderForm.reset({
      number: '',
      productId: this.store.products()[0]?.id ?? '',
      quantity: 1,
    });
    this.editorOpen.set(true);
  }

  protected startEdit(order: ProductionOrder): void {
    this.store.clearError();
    this.editingId.set(order.id);
    this.orderForm.reset({
      number: order.number,
      productId: order.productId,
      quantity: order.quantity,
    });
    this.editorOpen.set(true);
  }

  protected cancelEdit(): void {
    this.editorOpen.set(false);
    this.editingId.set(null);
  }

  protected async save(): Promise<void> {
    if (this.orderForm.invalid) {
      this.orderForm.markAllAsTouched();
      return;
    }
    const input: ProductionOrderInput = this.orderForm.getRawValue();
    if (await this.store.save(this.editingId(), input)) this.cancelEdit();
  }

  protected requestDelete(orderId: string): void {
    this.confirmDeleteId.set(orderId);
  }

  protected async confirmDelete(orderId: string): Promise<void> {
    if (await this.store.delete(orderId)) this.confirmDeleteId.set(null);
  }

  protected statusLabel(status: ProductionOrderStatus): string {
    const labels: Record<ProductionOrderStatus, string> = {
      planned: 'Đã lên kế hoạch',
      released: 'Đã phát hành',
      in_progress: 'Đang sản xuất',
      completed: 'Hoàn thành',
      cancelled: 'Đã hủy',
    };
    return labels[status];
  }

  protected async checkMaterials(order: ProductionOrder): Promise<void> {
    this.requirementOrder.set(order);
    this.requirementsOpen.set(true);
    await this.store.checkMaterials(order.id);
  }

  protected closeRequirements(): void {
    this.requirementsOpen.set(false);
    this.requirementOrder.set(null);
    this.store.clearRequirements();
  }

  protected openRelease(order: ProductionOrder): void {
    this.store.clearError();
    this.releaseConfirmOrder.set(order);
  }

  protected async confirmRelease(order: ProductionOrder): Promise<void> {
    if (await this.store.release(order.id)) this.releaseConfirmOrder.set(null);
  }

  protected async openStart(order: ProductionOrder): Promise<void> {
    this.store.clearError();
    this.startSubmitted.set(false);
    this.startConfirmationOpen.set(false);
    this.allocationDrafts.set({});
    this.startOrder.set(order);
    if (!(await this.store.prepareStart(order.id))) return;
    const defaultWarehouseId =
      this.activeWarehouses().length === 1 ? this.activeWarehouses()[0].id : '';
    const drafts: Record<string, AllocationDraft[]> = {};
    for (const material of this.store.requirements()?.materials ?? []) {
      drafts[material.materialId] = [
        { warehouseId: defaultWarehouseId, quantity: material.requiredQuantity },
      ];
    }
    this.allocationDrafts.set(drafts);
  }

  protected closeStart(): void {
    this.startOrder.set(null);
    this.startConfirmationOpen.set(false);
    this.startSubmitted.set(false);
    this.allocationDrafts.set({});
    this.store.clearRequirements();
    this.store.clearError();
  }

  protected allocationsFor(materialId: string): AllocationDraft[] {
    return this.allocationDrafts()[materialId] ?? [];
  }

  protected addAllocation(materialId: string): void {
    this.allocationDrafts.update((drafts) => ({
      ...drafts,
      [materialId]: [...(drafts[materialId] ?? []), { warehouseId: '', quantity: 0 }],
    }));
  }

  protected removeAllocation(materialId: string, index: number): void {
    this.allocationDrafts.update((drafts) => ({
      ...drafts,
      [materialId]: (drafts[materialId] ?? []).filter((_, itemIndex) => itemIndex !== index),
    }));
  }

  protected updateAllocationWarehouse(materialId: string, index: number, event: Event): void {
    this.updateAllocation(materialId, index, {
      warehouseId: (event.target as HTMLSelectElement).value,
    });
  }

  protected updateAllocationQuantity(materialId: string, index: number, event: Event): void {
    this.updateAllocation(materialId, index, {
      quantity: Number((event.target as HTMLInputElement).value),
    });
  }

  protected allocationTotal(materialId: string): number {
    return this.allocationsFor(materialId).reduce(
      (total, allocation) =>
        total + (Number.isFinite(allocation.quantity) ? allocation.quantity : 0),
      0,
    );
  }

  protected requestStartConfirmation(): void {
    this.startSubmitted.set(true);
    if (this.startReady()) this.startConfirmationOpen.set(true);
  }

  protected async confirmStart(order: ProductionOrder): Promise<void> {
    const allocations: ProductionMaterialAllocationInput[] = [];
    for (const [materialId, rows] of Object.entries(this.allocationDrafts())) {
      for (const row of rows) {
        allocations.push({ materialId, warehouseId: row.warehouseId, quantity: row.quantity });
      }
    }
    if (await this.store.start(order.id, { allocations })) this.closeStart();
  }

  protected openCancel(order: ProductionOrder): void {
    this.store.clearError();
    this.cancelConfirmOrder.set(order);
  }

  protected async confirmCancel(order: ProductionOrder): Promise<void> {
    if (await this.store.cancel(order.id)) this.cancelConfirmOrder.set(null);
  }

  protected openComplete(order: ProductionOrder): void {
    this.store.clearError();
    const warehouseId = this.activeWarehouses().length === 1 ? this.activeWarehouses()[0].id : '';
    this.completeForm.reset({ warehouseId });
    this.completeOrder.set(order);
  }

  protected async confirmComplete(order: ProductionOrder): Promise<void> {
    if (this.completeForm.invalid) {
      this.completeForm.markAllAsTouched();
      return;
    }
    if (await this.store.complete(order.id, this.completeForm.getRawValue())) {
      this.completeOrder.set(null);
    }
  }

  protected async openExecution(order: ProductionOrder): Promise<void> {
    this.store.clearError();
    this.executionOrderId.set(order.id);
    this.selectedMachineIds.set({});
    await this.store.loadExecution(order.id);
    const executable = this.store
      .operations()
      .find((operation) => this.canStartOperation(order, operation));
    const firstEligible = executable ? this.eligibleMachines(executable)[0] : null;
    if (executable && firstEligible) {
      this.selectedMachineIds.set({ [executable.id]: firstEligible.id });
    }
  }

  protected closeExecution(): void {
    this.executionOrderId.set(null);
    this.store.clearOperations();
  }

  protected canStartOperation(
    order: ProductionOrder,
    operation: ProductionOrderOperation,
  ): boolean {
    const operations = this.store.operations();
    return (
      order.status === 'in_progress' &&
      operation.status === 'pending' &&
      !operations.some((item) => item.status === 'in_progress') &&
      operations.find((item) => item.status !== 'completed')?.id === operation.id
    );
  }

  protected eligibleMachines(operation: ProductionOrderOperation) {
    return this.store
      .machines()
      .filter(
        (machine) =>
          machine.workCenterId === operation.workCenterId && machine.status === 'available',
      );
  }

  protected selectedMachineId(operationId: string): string {
    return this.selectedMachineIds()[operationId] ?? '';
  }

  protected selectMachine(operationId: string, event: Event): void {
    const machineId = (event.target as HTMLSelectElement).value;
    this.selectedMachineIds.update((selections) => ({
      ...selections,
      [operationId]: machineId,
    }));
  }

  protected async startOperation(
    order: ProductionOrder,
    operation: ProductionOrderOperation,
  ): Promise<void> {
    const machineId = this.selectedMachineId(operation.id);
    if (machineId) await this.store.startOperation(order.id, operation.id, machineId);
  }

  protected async completeOperation(
    order: ProductionOrder,
    operation: ProductionOrderOperation,
  ): Promise<void> {
    await this.store.completeOperation(order.id, operation.id);
  }

  protected operationStatusLabel(status: ProductionOrderOperation['status']): string {
    return {
      pending: 'Chờ thực hiện',
      in_progress: 'Đang thực hiện',
      completed: 'Hoàn thành',
    }[status];
  }

  protected warehouseLabel(warehouse: Warehouse): string {
    return `${warehouse.code} — ${warehouse.name}`;
  }

  private updateAllocation(
    materialId: string,
    index: number,
    changes: Partial<AllocationDraft>,
  ): void {
    this.allocationDrafts.update((drafts) => ({
      ...drafts,
      [materialId]: (drafts[materialId] ?? []).map((row, itemIndex) =>
        itemIndex === index ? { ...row, ...changes } : row,
      ),
    }));
  }

  protected roundQuantity(value: number): number {
    return Math.round((value + Number.EPSILON) * 1_000_000) / 1_000_000;
  }
}
