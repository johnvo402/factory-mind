import { Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { DialogFocusDirective } from '../../shared/ui/dialog-focus.directive';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { businessDataErrorMessage } from '../data/business-data-error';
import { Product } from '../products/product.models';
import { WorkCenterApiService } from '../work-centers/work-center-api.service';
import { WorkCenter } from '../work-centers/work-center.models';
import { RoutingApiService } from './routing-api.service';
import { Routing, RoutingInput, RoutingOperation, RoutingStatus } from './routing.models';

@Component({
  selector: 'app-product-routing-panel',
  imports: [ReactiveFormsModule, DialogFocusDirective, UiIconComponent],
  templateUrl: './product-routing-panel.component.html',
  styleUrl: './product-routing-panel.component.scss',
})
export class ProductRoutingPanelComponent implements OnInit {
  private readonly api = inject(RoutingApiService);
  private readonly workCenterApi = inject(WorkCenterApiService);
  readonly product = input.required<Product>();
  readonly closed = output<void>();
  protected readonly routings = signal<Routing[]>([]);
  protected readonly workCenters = signal<WorkCenter[]>([]);
  protected readonly selectedId = signal<string | null>(null);
  protected readonly editingId = signal<string | null>(null);
  protected readonly editing = signal(false);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal('');
  protected readonly selected = computed(() => this.routings().find(item => item.id === this.selectedId()) ?? null);
  protected readonly form = new FormGroup({ operations: new FormArray<RoutingOperationForm>([]) });

  ngOnInit(): void { void this.load(); }

  protected async load(preferredId?: string): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const [routingResponse, workCenterResponse] = await Promise.all([
        firstValueFrom(this.api.list(this.product().id)),
        firstValueFrom(this.workCenterApi.getWorkCenters()),
      ]);
      this.routings.set(routingResponse.data ?? []);
      this.workCenters.set(workCenterResponse.data ?? []);
      this.selectedId.set(preferredId ?? this.routings().find(item => item.status === 'active')?.id ?? this.routings()[0]?.id ?? null);
    } catch (error: unknown) {
      this.error.set(businessDataErrorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }

  protected createDraft(): void {
    const source = this.selected();
    this.editingId.set(null);
    this.form.controls.operations.clear();
    (source?.operations ?? []).forEach(operation => this.addOperation(operation));
    if (this.form.controls.operations.length === 0) this.addOperation();
    this.editing.set(true);
  }

  protected editDraft(routing: Routing): void {
    if (routing.status !== 'draft') return;
    this.editingId.set(routing.id);
    this.form.controls.operations.clear();
    routing.operations.forEach(operation => this.addOperation(operation));
    this.editing.set(true);
  }

  protected addOperation(operation?: RoutingOperation): void {
    this.form.controls.operations.push(new FormGroup({
      sequence: new FormControl(operation?.sequence ?? (this.form.controls.operations.length + 1) * 10, { nonNullable: true, validators: [Validators.required, Validators.min(1)] }),
      name: new FormControl(operation?.name ?? '', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
      workCenterId: new FormControl(operation?.workCenterId ?? this.activeWorkCenters()[0]?.id ?? '', { nonNullable: true, validators: [Validators.required] }),
      setupTimeMinutes: new FormControl(operation?.setupTimeMinutes ?? 0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
      runTimeMinutes: new FormControl(operation?.runTimeMinutes ?? 0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
      description: new FormControl<string | null>(operation?.description ?? null, [Validators.maxLength(500)]),
    }));
  }

  protected activeWorkCenters(): WorkCenter[] { return this.workCenters().filter(item => item.isActive); }
  protected removeOperation(index: number): void { this.form.controls.operations.removeAt(index); }

  protected async save(): Promise<void> {
    const values = this.form.controls.operations.getRawValue();
    const duplicate = new Set(values.map(item => item.sequence)).size !== values.length;
    this.form.controls.operations.setErrors(duplicate ? { duplicateSequence: true } : null);
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const input: RoutingInput = { operations: values };
    this.saving.set(true);
    this.error.set('');
    try {
      const editingId = this.editingId();
      const response = await firstValueFrom(editingId
        ? this.api.update(this.product().id, editingId, input)
        : this.api.create(this.product().id, input));
      this.editing.set(false);
      await this.load(response.data?.id);
    } catch (error: unknown) {
      this.error.set(businessDataErrorMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected async activate(routing: Routing): Promise<void> {
    this.saving.set(true);
    this.error.set('');
    try {
      await firstValueFrom(this.api.activate(this.product().id, routing.id));
      await this.load(routing.id);
    } catch (error: unknown) {
      this.error.set(businessDataErrorMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected statusLabel(status: RoutingStatus): string {
    return { draft: 'Bản nháp', active: 'Đang dùng', archived: 'Đã lưu trữ' }[status];
  }
}

type RoutingOperationForm = FormGroup<{
  sequence: FormControl<number>;
  name: FormControl<string>;
  workCenterId: FormControl<string>;
  setupTimeMinutes: FormControl<number>;
  runTimeMinutes: FormControl<number>;
  description: FormControl<string | null>;
}>;
