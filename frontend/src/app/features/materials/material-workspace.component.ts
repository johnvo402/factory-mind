import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Material, MaterialInput } from './material.models';
import { MaterialStore } from './material.store';

@Component({
  selector: 'app-material-workspace',
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './material-workspace.component.html',
  styleUrl: '../data/entity-workspace.scss',
})
export class MaterialWorkspaceComponent implements OnInit {
  protected readonly store = inject(MaterialStore);
  protected readonly editingId = signal<string | null>(null);
  protected readonly editorOpen = signal(false);
  protected readonly confirmDeleteId = signal<string | null>(null);
  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly materialForm = new FormGroup({
    code: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(50)],
    }),
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    unit: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(30)],
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
    this.materialForm.reset({ code: '', name: '', unit: '' });
    this.editorOpen.set(true);
  }

  protected startEdit(material: Material): void {
    this.store.clearError();
    this.editingId.set(material.id);
    this.materialForm.reset({
      code: material.code,
      name: material.name,
      unit: material.unit,
    });
    this.editorOpen.set(true);
  }

  protected cancelEdit(): void {
    this.editorOpen.set(false);
    this.editingId.set(null);
  }

  protected async save(): Promise<void> {
    if (this.materialForm.invalid) {
      this.materialForm.markAllAsTouched();
      return;
    }

    const input: MaterialInput = this.materialForm.getRawValue();
    if (await this.store.save(this.editingId(), input)) {
      this.cancelEdit();
    }
  }

  protected requestDelete(materialId: string): void {
    this.confirmDeleteId.set(materialId);
  }

  protected async confirmDelete(materialId: string): Promise<void> {
    if (await this.store.delete(materialId)) {
      this.confirmDeleteId.set(null);
    }
  }
}
