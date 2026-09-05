import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { DialogFocusDirective } from '../../shared/ui/dialog-focus.directive';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import {
  ProductionOrder,
  ProductionOrderInput,
  ProductionOrderOperation,
  ProductionOrderStatus,
} from './production-order.models';
import { ProductionOrderStore } from './production-order.store';

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
  protected readonly executionOrderId = signal<string | null>(null);
  protected readonly executionOrder = computed(() =>
    this.store.orders().find(order => order.id === this.executionOrderId()) ?? null,
  );
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
    if (await this.store.save(this.editingId(), input)) {
      this.cancelEdit();
    }
  }

  protected requestDelete(orderId: string): void {
    this.confirmDeleteId.set(orderId);
  }

  protected async confirmDelete(orderId: string): Promise<void> {
    if (await this.store.delete(orderId)) {
      this.confirmDeleteId.set(null);
    }
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

  protected openExecution(order: ProductionOrder): void { this.executionOrderId.set(order.id); }
  protected closeExecution(): void { this.executionOrderId.set(null); }
  protected canStartOperation(order: ProductionOrder, operation: ProductionOrderOperation): boolean {
    return order.status === 'in_progress' && operation.status === 'pending' &&
      !order.operations.some(item => item.status === 'in_progress') &&
      order.operations.find(item => item.status !== 'completed')?.id === operation.id;
  }
  protected async startOperation(order: ProductionOrder, operation: ProductionOrderOperation): Promise<void> {
    await this.store.startOperation(order.id, operation.id);
  }
  protected async completeOperation(order: ProductionOrder, operation: ProductionOrderOperation): Promise<void> {
    await this.store.completeOperation(order.id, operation.id);
  }
  protected operationStatusLabel(status: ProductionOrderOperation['status']): string {
    return { pending: 'Chờ thực hiện', in_progress: 'Đang thực hiện', completed: 'Hoàn thành' }[status];
  }
}
