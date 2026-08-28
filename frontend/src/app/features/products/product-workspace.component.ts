import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import {
  FormArray,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { Bom, BomInput, BomItem, BomStatus } from '../boms/bom.models';
import { BomStore } from '../boms/bom.store';
import { Product, ProductInput } from './product.models';
import { ProductStore } from './product.store';

@Component({
  selector: 'app-product-workspace',
  imports: [DatePipe, DecimalPipe, ReactiveFormsModule],
  templateUrl: './product-workspace.component.html',
  styleUrls: ['../data/entity-workspace.scss', './product-workspace.component.scss'],
})
export class ProductWorkspaceComponent implements OnInit {
  protected readonly store = inject(ProductStore);
  protected readonly bomStore = inject(BomStore);
  protected readonly editingId = signal<string | null>(null);
  protected readonly editorOpen = signal(false);
  protected readonly confirmDeleteId = signal<string | null>(null);
  protected readonly bomPanelOpen = signal(false);
  protected readonly bomEditorOpen = signal(false);
  protected readonly selectedProduct = signal<Product | null>(null);
  protected readonly selectedBomId = signal<string | null>(null);
  protected readonly editingBomId = signal<string | null>(null);
  protected readonly allMaterialsUsed = signal(false);
  protected readonly selectedBom = computed(() =>
    this.bomStore.boms().find((bom) => bom.id === this.selectedBomId()) ?? null,
  );
  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly requirementQuantity = new FormControl(100, {
    nonNullable: true,
    validators: [Validators.required, Validators.min(0.000001)],
  });
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
  protected readonly bomForm = new FormGroup({
    outputQuantity: new FormControl(1, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0.000001)],
    }),
    items: new FormArray<BomItemForm>([]),
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

  protected async openBoms(product: Product): Promise<void> {
    this.selectedProduct.set(product);
    this.selectedBomId.set(null);
    this.bomEditorOpen.set(false);
    this.bomPanelOpen.set(true);
    await this.bomStore.load(product.id);
    const preferred =
      this.bomStore.boms().find((bom) => bom.status === 'active') ?? this.bomStore.boms()[0];
    this.selectedBomId.set(preferred?.id ?? null);
  }

  protected closeBoms(): void {
    this.bomPanelOpen.set(false);
    this.bomEditorOpen.set(false);
    this.selectedProduct.set(null);
    this.selectedBomId.set(null);
    this.bomStore.clear();
  }

  protected selectBom(bom: Bom): void {
    this.selectedBomId.set(bom.id);
    this.bomEditorOpen.set(false);
    this.bomStore.clearError();
  }

  protected startNewRevision(): void {
    const source =
      this.selectedBom() ?? this.bomStore.boms().find((bom) => bom.status === 'active') ?? null;
    this.editingBomId.set(null);
    this.populateBomForm(source?.outputQuantity ?? 1, source?.items ?? []);
    this.bomEditorOpen.set(true);
    this.bomStore.clearError();
  }

  protected startEditBom(bom: Bom): void {
    if (bom.status !== 'draft') return;
    this.editingBomId.set(bom.id);
    this.populateBomForm(bom.outputQuantity, bom.items);
    this.bomEditorOpen.set(true);
    this.bomStore.clearError();
  }

  protected cancelBomEdit(): void {
    this.bomEditorOpen.set(false);
    this.editingBomId.set(null);
    this.allMaterialsUsed.set(false);
  }

  protected addBomItem(item?: BomItem): void {
    const selectedMaterialIds = new Set(
      this.bomForm.controls.items.controls.map((control) => control.controls.materialId.value),
    );
    const materialId =
      item?.materialId ??
      this.bomStore.materials().find((material) => !selectedMaterialIds.has(material.id))?.id;
    if (!materialId) {
      this.allMaterialsUsed.set(true);
      return;
    }

    this.allMaterialsUsed.set(false);
    this.bomForm.controls.items.push(
      new FormGroup({
        materialId: new FormControl(materialId, {
          nonNullable: true,
          validators: [Validators.required],
        }),
        quantity: new FormControl(item?.quantity ?? 1, {
          nonNullable: true,
          validators: [Validators.required, Validators.min(0.000001)],
        }),
        scrapPercentage: new FormControl<number | null>(item?.scrapPercentage ?? null, {
          validators: [Validators.min(0), Validators.max(100)],
        }),
      }),
    );
  }

  protected removeBomItem(index: number): void {
    this.bomForm.controls.items.removeAt(index);
    this.allMaterialsUsed.set(false);
  }

  protected async saveBom(): Promise<void> {
    const product = this.selectedProduct();
    if (!product) return;

    const items = this.bomForm.controls.items.getRawValue();
    const duplicateMaterial = new Set(items.map((item) => item.materialId)).size !== items.length;
    this.bomForm.controls.items.setErrors(duplicateMaterial ? { duplicateMaterial: true } : null);
    if (this.bomForm.invalid) {
      this.bomForm.markAllAsTouched();
      return;
    }

    const input: BomInput = {
      outputQuantity: this.bomForm.controls.outputQuantity.value,
      items,
    };
    const saved = await this.bomStore.save(product.id, this.editingBomId(), input);
    if (saved) {
      this.selectedBomId.set(saved.id);
      this.cancelBomEdit();
    }
  }

  protected async activateBom(bom: Bom): Promise<void> {
    const product = this.selectedProduct();
    if (!product) return;
    const activated = await this.bomStore.activate(product.id, bom.id);
    if (activated) this.selectedBomId.set(activated.id);
  }

  protected async archiveBom(bom: Bom): Promise<void> {
    const product = this.selectedProduct();
    if (!product) return;
    const archived = await this.bomStore.archive(product.id, bom.id);
    if (archived) this.selectedBomId.set(archived.id);
  }

  protected async calculateRequirements(): Promise<void> {
    const product = this.selectedProduct();
    if (!product || this.requirementQuantity.invalid) {
      this.requirementQuantity.markAsTouched();
      return;
    }

    await this.bomStore.calculate(product.id, this.requirementQuantity.value);
  }

  protected bomStatusLabel(status: BomStatus): string {
    return { draft: 'Bản nháp', active: 'Đang dùng', archived: 'Đã lưu trữ' }[status];
  }

  protected materialUnit(materialId: string): string {
    return this.bomStore.materials().find((material) => material.id === materialId)?.unit ?? '';
  }

  private populateBomForm(outputQuantity: number, items: BomItem[]): void {
    this.bomForm.controls.outputQuantity.setValue(outputQuantity);
    this.bomForm.controls.items.clear();
    items.forEach((item) => this.addBomItem(item));
    this.bomForm.controls.items.setErrors(null);
    this.allMaterialsUsed.set(false);
  }
}

type BomItemForm = FormGroup<{
  materialId: FormControl<string>;
  quantity: FormControl<number>;
  scrapPercentage: FormControl<number | null>;
}>;
