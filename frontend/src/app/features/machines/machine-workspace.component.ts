import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { DialogFocusDirective } from '../../shared/ui/dialog-focus.directive';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { Machine, MachineInput, MachineStatus } from './machine.models';
import { MachineStore } from './machine.store';

@Component({
  selector: 'app-machine-workspace',
  imports: [DatePipe, ReactiveFormsModule, DialogFocusDirective, UiIconComponent],
  templateUrl: './machine-workspace.component.html',
  styleUrls: ['../data/entity-workspace.scss', './machine-workspace.component.scss'],
})
export class MachineWorkspaceComponent implements OnInit {
  protected readonly store = inject(MachineStore);
  protected readonly editingId = signal<string | null>(null);
  protected readonly editorOpen = signal(false);
  protected readonly confirmDeleteId = signal<string | null>(null);
  protected readonly statuses: ReadonlyArray<{ value: MachineStatus; label: string }> = [
    { value: 'available', label: 'Sẵn sàng' },
    { value: 'running', label: 'Đang chạy' },
    { value: 'maintenance', label: 'Bảo trì' },
    { value: 'offline', label: 'Ngoại tuyến' },
  ];
  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly machineForm = new FormGroup({
    code: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(50)],
    }),
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    status: new FormControl<MachineStatus>('available', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  ngOnInit(): void {
    void this.store.load();
  }

  protected search(event?: SubmitEvent): void {
    event?.preventDefault();
    void this.store.load(this.searchControl.getRawValue());
  }

  protected clearSearch(): void {
    this.searchControl.setValue('');
    void this.store.load('');
  }

  protected startCreate(): void {
    this.store.clearError();
    this.editingId.set(null);
    this.machineForm.reset({ code: '', name: '', status: 'available' });
    this.editorOpen.set(true);
  }

  protected startEdit(machine: Machine): void {
    this.store.clearError();
    this.editingId.set(machine.id);
    this.machineForm.reset({
      code: machine.code,
      name: machine.name,
      status: machine.status,
    });
    this.editorOpen.set(true);
  }

  protected cancelEdit(): void {
    this.editorOpen.set(false);
    this.editingId.set(null);
  }

  protected async save(): Promise<void> {
    if (this.machineForm.invalid) {
      this.machineForm.markAllAsTouched();
      return;
    }

    const input: MachineInput = this.machineForm.getRawValue();
    if (await this.store.save(this.editingId(), input)) {
      this.cancelEdit();
    }
  }

  protected requestDelete(machineId: string): void {
    this.confirmDeleteId.set(machineId);
  }

  protected cancelDelete(): void {
    this.confirmDeleteId.set(null);
  }

  protected async confirmDelete(machineId: string): Promise<void> {
    if (await this.store.delete(machineId)) {
      this.confirmDeleteId.set(null);
    }
  }

  protected statusLabel(status: MachineStatus): string {
    return this.statuses.find(item => item.value === status)?.label ?? status;
  }
}
