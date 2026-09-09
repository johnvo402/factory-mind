import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { NgStyle } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { businessDataErrorMessage } from '../data/business-data-error';
import { PlanningApiService } from './planning-api.service';
import {
  ScheduleOperationPreview,
  ScheduleOrderPreview,
  SchedulePreview,
  UnscheduledOperationPreview,
  WorkCenterCapacityPreview,
} from './planning.models';

@Component({
  selector: 'app-planning-workspace',
  imports: [NgStyle, UiIconComponent],
  templateUrl: './planning-workspace.component.html',
  styleUrl: './planning-workspace.component.scss',
})
export class PlanningWorkspaceComponent implements OnInit {
  private readonly api = inject(PlanningApiService);
  protected readonly preview = signal<SchedulePreview | null>(null);
  protected readonly requestedHorizonDays = signal(14);
  protected readonly loading = signal(false);
  protected readonly error = signal('');
  protected readonly selectedOperation = signal<ScheduleOperationPreview | null>(null);
  protected readonly loadedHorizonDays = computed(() => {
    const preview = this.preview();
    if (!preview) return this.requestedHorizonDays();
    const days = (Date.parse(preview.horizonEnd) - Date.parse(preview.horizonStart)) / 86_400_000;
    return Number.isFinite(days) && days > 0 ? Math.round(days) : this.requestedHorizonDays();
  });
  protected readonly selectedOrder = computed(() => {
    const operation = this.selectedOperation();
    return operation ? this.preview()?.orders.find(order => order.operations.some(item => item.id === operation.id)) ?? null : null;
  });
  protected readonly days = computed(() => {
    const preview = this.preview();
    if (!preview) return [];
    return Array.from({ length: this.loadedHorizonDays() }, (_, index) => {
      const date = new Date(preview.horizonStart);
      date.setUTCDate(date.getUTCDate() + index);
      return date;
    });
  });

  ngOnInit(): void { void this.load(); }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const response = await firstValueFrom(this.api.getPreview(this.requestedHorizonDays()));
      this.preview.set(response.data ?? null);
      this.selectedOperation.set(null);
    } catch (error: unknown) {
      this.error.set(businessDataErrorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }

  protected changeHorizon(value: number): void {
    if (this.requestedHorizonDays() === value) return;
    this.requestedHorizonDays.set(value);
    void this.load();
  }

  protected operationsFor(centerId: string, lane: number): Array<{ order: ScheduleOrderPreview; operation: ScheduleOperationPreview }> {
    return (this.preview()?.orders ?? []).flatMap(order => order.operations
      .filter(operation => operation.workCenterId === centerId && operation.lane === lane)
      .map(operation => ({ order, operation })));
  }

  protected laneNumbers(center: WorkCenterCapacityPreview): number[] {
    return Array.from({ length: center.parallelCapacity }, (_, index) => index + 1);
  }

  protected barStyle(operation: ScheduleOperationPreview): Record<string, string> {
    const preview = this.preview();
    if (!preview) return {};
    const start = new Date(preview.horizonStart).getTime();
    const span = new Date(preview.horizonEnd).getTime() - start;
    const left = Math.max(0, (new Date(operation.scheduledStart).getTime() - start) / span * 100);
    const width = Math.max(.35, (new Date(operation.scheduledEnd).getTime() - new Date(operation.scheduledStart).getTime()) / span * 100);
    return { left: `${left}%`, width: `${Math.min(width, 100 - left)}%` };
  }

  protected formatDate(value: string | Date | null, includeTime = true): string {
    if (!value) return '—';
    const timeZone = this.preview()?.timeZoneId ?? 'UTC';
    return new Intl.DateTimeFormat('vi-VN', {
      timeZone,
      day: '2-digit', month: '2-digit', year: 'numeric',
      ...(includeTime ? { hour: '2-digit', minute: '2-digit' } : {}),
    }).format(new Date(value));
  }

  protected projectedLabel(status: ScheduleOrderPreview['projectedDeliveryStatus']): string {
    return { unknown: 'Chưa xác định', projected_on_time: 'Dự kiến đúng hạn', projected_late: 'Dự kiến trễ' }[status];
  }

  protected deliveryLabel(status: string): string {
    const labels: Record<string, string> = {
      no_due_date: 'Không có hạn', on_track: 'Đang đúng hạn', due_soon: 'Sắp đến hạn', overdue: 'Quá hạn',
      completed_on_time: 'Hoàn thành đúng hạn', completed_late: 'Hoàn thành trễ', cancelled: 'Đã hủy',
    };
    return labels[status] ?? status;
  }

  protected capacityStatus(center: WorkCenterCapacityPreview): string {
    if (!center.isActive) return 'Ngừng hoạt động';
    if (!center.hasCalendar) return 'Thiếu lịch làm việc';
    if (center.hasCapacityConstraint) return 'Thiếu năng lực trong horizon';
    if ((center.plannedLoadPercent ?? 0) >= 85) return 'Áp lực năng lực cao';
    return 'Trong năng lực preview';
  }

  protected reasonLabel(item: UnscheduledOperationPreview): string {
    const center = item.workCenterCode ? `${item.workCenterCode}: ` : '';
    const labels: Record<string, string> = {
      active_routing_missing: 'Chưa có Routing active cho lệnh Planned.',
      operation_snapshot_missing: 'Thiếu snapshot công đoạn đã khóa.',
      work_center_missing: 'Không tìm thấy Work Center.',
      work_center_inactive: `${center}Work Center đã ngừng hoạt động.`,
      calendar_missing: `${center}chưa cấu hình ca làm việc.`,
      horizon_exceeded: `Không đủ thời gian trong khung ${this.loadedHorizonDays()} ngày.`,
      current_capacity_conflict: `${center}số công đoạn đang chạy vượt parallel capacity.`,
      blocked_by_predecessor: 'Chưa thể lập lịch vì thời điểm hoàn thành công đoạn trước chưa xác định.',
    };
    return labels[item.reason] ?? item.reason;
  }
}
