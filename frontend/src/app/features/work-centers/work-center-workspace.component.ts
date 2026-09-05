import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { DialogFocusDirective } from '../../shared/ui/dialog-focus.directive';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { businessDataErrorMessage } from '../data/business-data-error';
import { WorkCenterApiService } from './work-center-api.service';
import { WorkCenter, WorkCenterInput } from './work-center.models';

@Component({
  selector: 'app-work-center-workspace',
  imports: [ReactiveFormsModule, DialogFocusDirective, UiIconComponent],
  templateUrl: './work-center-workspace.component.html',
  styleUrls: ['../data/entity-workspace.scss', './work-center-workspace.component.scss'],
})
export class WorkCenterWorkspaceComponent implements OnInit {
  private readonly api = inject(WorkCenterApiService);
  protected readonly workCenters = signal<WorkCenter[]>([]);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal('');
  protected readonly editorOpen = signal(false);
  protected readonly editingId = signal<string | null>(null);
  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly form = new FormGroup({
    code: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(50)] }),
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
    description: new FormControl<string | null>(null, [Validators.maxLength(500)]),
  });

  ngOnInit(): void { void this.load(); }

  protected async load(search = this.searchControl.value): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const response = await firstValueFrom(this.api.getWorkCenters(search.trim()));
      this.workCenters.set(response.data ?? []);
    } catch (error: unknown) {
      this.error.set(businessDataErrorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }

  protected openCreate(): void {
    this.editingId.set(null);
    this.form.reset({ code: '', name: '', description: null });
    this.editorOpen.set(true);
  }

  protected openEdit(workCenter: WorkCenter): void {
    this.editingId.set(workCenter.id);
    this.form.reset({ code: workCenter.code, name: workCenter.name, description: workCenter.description });
    this.editorOpen.set(true);
  }

  protected async save(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.error.set('');
    const input: WorkCenterInput = this.form.getRawValue();
    try {
      const id = this.editingId();
      await firstValueFrom(id ? this.api.update(id, input) : this.api.create(input));
      this.editorOpen.set(false);
      await this.load();
    } catch (error: unknown) {
      this.error.set(businessDataErrorMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected async deactivate(workCenter: WorkCenter): Promise<void> {
    this.saving.set(true);
    this.error.set('');
    try {
      await firstValueFrom(this.api.deactivate(workCenter.id));
      await this.load();
    } catch (error: unknown) {
      this.error.set(businessDataErrorMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
}
