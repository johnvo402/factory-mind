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
  protected readonly editingRunningMachine = signal(false);
  protected readonly statuses: ReadonlyArray<{ value: MachineStatus; label: string }> = [
    { value: 'available', label: 'Sẵn sàng' },
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
    workCenterId: new FormControl<string | null>(null),
  });

  ngOnInit(): void {
    void this.store.initialize();
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
    this.editingRunningMachine.set(false);
    this.machineForm.reset({ code: '', name: '', status: 'available', workCenterId: null });
    this.editorOpen.set(true);
  }

  protected startEdit(machine: Machine): void {
    this.store.clearError();
    this.editingId.set(machine.id);
    this.editingRunningMachine.set(machine.status === 'running');
    this.machineForm.reset({
      code: machine.code,
      name: machine.name,
      status: machine.status === 'running' ? 'available' : machine.status,
      workCenterId: machine.workCenterId,
    });
    this.editorOpen.set(true);
  }

  protected cancelEdit(): void {
    this.editorOpen.set(false);
    this.editingId.set(null);
    this.editingRunningMachine.set(false);
  }

  protected async save(): Promise<void> {
    if (this.machineForm.invalid) {
      this.machineForm.markAllAsTouched();
      return;
    }

    const value = this.machineForm.getRawValue();
    const input: MachineInput = { ...value, workCenterId: value.workCenterId || null };
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
    return status === 'running'
      ? 'Đang chạy'
      : this.statuses.find(item => item.value === status)?.label ?? status;
  }
}
