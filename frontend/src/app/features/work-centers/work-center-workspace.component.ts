import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { DialogFocusDirective } from '../../shared/ui/dialog-focus.directive';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { businessDataErrorMessage } from '../data/business-data-error';
import { WorkCenterApiService } from './work-center-api.service';
import { WorkCenter, WorkCenterDayOff, WorkCenterInput, WorkCenterShift } from './work-center.models';

export function validateWorkCenterCalendar(
  parallelCapacity: number,
  shifts: WorkCenterShift[],
  daysOff: WorkCenterDayOff[],
): string {
  if (parallelCapacity < 1 || parallelCapacity > 100) return 'Parallel capacity phải từ 1 đến 100.';
  for (const shift of shifts) {
    if (!shift.startTime || !shift.endTime || shift.startTime >= shift.endTime) return 'Giờ bắt đầu phải trước giờ kết thúc.';
  }
  const groups = new Map<number, WorkCenterShift[]>();
  for (const shift of shifts) groups.set(shift.dayOfWeek, [...(groups.get(shift.dayOfWeek) ?? []), shift]);
  for (const items of groups.values()) {
    const ordered = [...items].sort((a, b) => a.startTime.localeCompare(b.startTime));
    if (ordered.some((item, index) => index > 0 && ordered[index - 1].endTime > item.startTime)) return 'Các ca trong cùng ngày không được chồng lấn.';
  }
  if (new Set(daysOff.map(day => day.date)).size !== daysOff.length) return 'Ngày nghỉ không được trùng.';
  return '';
}

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
  protected readonly calendarOpen = signal(false);
  protected readonly calendarWorkCenter = signal<WorkCenter | null>(null);
  protected readonly parallelCapacity = signal(1);
  protected readonly shifts = signal<WorkCenterShift[]>([]);
  protected readonly daysOff = signal<WorkCenterDayOff[]>([]);
  protected readonly newDayOff = signal('');
  protected readonly calendarTimeZone = signal('UTC');
  protected readonly calendarValidation = computed(() => this.validateCalendar());
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

  protected async openCalendar(workCenter: WorkCenter): Promise<void> {
    this.saving.set(true);
    this.error.set('');
    try {
      const response = await firstValueFrom(this.api.getCalendar(workCenter.id));
      const calendar = response.data!;
      this.calendarWorkCenter.set(workCenter);
      this.parallelCapacity.set(calendar.parallelCapacity);
      this.shifts.set(calendar.shifts.map(shift => ({ ...shift })));
      this.daysOff.set(calendar.daysOff.map(day => ({ ...day })));
      this.calendarTimeZone.set(calendar.timeZoneId);
      this.newDayOff.set('');
      this.calendarOpen.set(true);
    } catch (error: unknown) {
      this.error.set(businessDataErrorMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected addShift(): void {
    this.shifts.update(items => [...items, { dayOfWeek: 1, startTime: '08:00:00', endTime: '17:00:00' }]);
  }

  protected updateShift(index: number, field: keyof WorkCenterShift, event: Event): void {
    const raw = (event.target as HTMLInputElement | HTMLSelectElement).value;
    this.shifts.update(items => items.map((item, itemIndex) => itemIndex === index
      ? { ...item, [field]: field === 'dayOfWeek' ? Number(raw) : (raw.length === 5 ? `${raw}:00` : raw) }
      : item));
  }

  protected removeShift(index: number): void {
    this.shifts.update(items => items.filter((_, itemIndex) => itemIndex !== index));
  }

  protected addDayOff(): void {
    const date = this.newDayOff();
    if (!date || this.daysOff().some(day => day.date === date)) return;
    this.daysOff.update(items => [...items, { date }].sort((a, b) => a.date.localeCompare(b.date)));
    this.newDayOff.set('');
  }

  protected removeDayOff(date: string): void {
    this.daysOff.update(items => items.filter(day => day.date !== date));
  }

  protected async saveCalendar(): Promise<void> {
    const workCenter = this.calendarWorkCenter();
    if (!workCenter || this.calendarValidation()) return;
    this.saving.set(true);
    this.error.set('');
    try {
      await firstValueFrom(this.api.replaceCalendar(workCenter.id, {
        parallelCapacity: this.parallelCapacity(),
        shifts: this.shifts(),
        daysOff: this.daysOff(),
      }));
      this.calendarOpen.set(false);
      await this.load();
    } catch (error: unknown) {
      this.error.set(businessDataErrorMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected dayLabel(day: number): string {
    return ['Chủ nhật', 'Thứ hai', 'Thứ ba', 'Thứ tư', 'Thứ năm', 'Thứ sáu', 'Thứ bảy'][day] ?? '';
  }

  private validateCalendar(): string {
    return validateWorkCenterCalendar(this.parallelCapacity(), this.shifts(), this.daysOff());
  }
}
