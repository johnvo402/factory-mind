import { DecimalPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Inventory, InventoryInput } from './inventory.models';
import { InventoryStore } from './inventory.store';

@Component({
  selector: 'app-inventory-workspace',
  imports: [DecimalPipe, ReactiveFormsModule],
  templateUrl: './inventory-workspace.component.html',
  styleUrl: '../data/entity-workspace.scss',
})
export class InventoryWorkspaceComponent implements OnInit {
  protected readonly store = inject(InventoryStore);
  protected readonly editingId = signal<string | null>(null);
  protected readonly editorOpen = signal(false);
  protected readonly confirmDeleteId = signal<string | null>(null);
  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly inventoryForm = new FormGroup({
    materialId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    warehouse: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(100)],
    }),
    quantity: new FormControl(0, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0)],
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
    this.inventoryForm.reset({
      materialId: this.store.materials()[0]?.id ?? '',
      warehouse: '',
      quantity: 0,
    });
    this.editorOpen.set(true);
  }

  protected startEdit(inventory: Inventory): void {
    this.store.clearError();
    this.editingId.set(inventory.id);
    this.inventoryForm.reset({
      materialId: inventory.materialId,
      warehouse: inventory.warehouse,
      quantity: inventory.quantity,
    });
    this.editorOpen.set(true);
  }

  protected cancelEdit(): void {
    this.editorOpen.set(false);
    this.editingId.set(null);
  }

  protected async save(): Promise<void> {
    if (this.inventoryForm.invalid) {
      this.inventoryForm.markAllAsTouched();
      return;
    }

    const input: InventoryInput = this.inventoryForm.getRawValue();
    if (await this.store.save(this.editingId(), input)) {
      this.cancelEdit();
    }
  }

  protected requestDelete(inventoryId: string): void {
    this.confirmDeleteId.set(inventoryId);
  }

  protected async confirmDelete(inventoryId: string): Promise<void> {
    if (await this.store.delete(inventoryId)) {
      this.confirmDeleteId.set(null);
    }
  }
}
