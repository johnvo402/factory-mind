import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Product, ProductInput } from './product.models';
import { ProductStore } from './product.store';

@Component({
  selector: 'app-product-workspace',
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './product-workspace.component.html',
  styleUrl: '../data/entity-workspace.scss',
})
export class ProductWorkspaceComponent implements OnInit {
  protected readonly store = inject(ProductStore);
  protected readonly editingId = signal<string | null>(null);
  protected readonly editorOpen = signal(false);
  protected readonly confirmDeleteId = signal<string | null>(null);
  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly productForm = new FormGroup({
    code: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(50)],
    }),
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
  });

  ngOnInit(): void {
    void this.store.load();
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
    this.productForm.reset({ code: '', name: '' });
    this.editorOpen.set(true);
  }

  protected startEdit(product: Product): void {
    this.store.clearError();
    this.editingId.set(product.id);
    this.productForm.reset({ code: product.code, name: product.name });
    this.editorOpen.set(true);
  }

  protected cancelEdit(): void {
    this.editorOpen.set(false);
    this.editingId.set(null);
  }

  protected async save(): Promise<void> {
    if (this.productForm.invalid) {
      this.productForm.markAllAsTouched();
      return;
    }

    const input: ProductInput = this.productForm.getRawValue();
    if (await this.store.save(this.editingId(), input)) {
      this.cancelEdit();
    }
  }

  protected requestDelete(productId: string): void {
    this.confirmDeleteId.set(productId);
  }

  protected async confirmDelete(productId: string): Promise<void> {
    if (await this.store.delete(productId)) {
      this.confirmDeleteId.set(null);
    }
  }
}
