import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { PlanningApiService } from './planning-api.service';
import { ScheduleOperationPreview, SchedulePreview } from './planning.models';
import { PlanningWorkspaceComponent } from './planning-workspace.component';

describe('PlanningWorkspaceComponent', () => {
  let fixture: ComponentFixture<PlanningWorkspaceComponent>;
  let api: jasmine.SpyObj<PlanningApiService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<PlanningApiService>('PlanningApiService', ['getPreview']);
    api.getPreview.and.returnValue(of({ success: true, message: 'OK', data: preview() }));
    await TestBed.configureTestingModule({
      imports: [PlanningWorkspaceComponent],
      providers: [{ provide: PlanningApiService, useValue: api }],
    }).compileComponents();
    fixture = TestBed.createComponent(PlanningWorkspaceComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('loads 14-day preview and renders capacity, provisional late bar, and warning', () => {
    expect(api.getPreview).toHaveBeenCalledWith(14);
    expect(text()).toContain('86.5%');
    expect(text()).toContain('Dự kiến · chưa khóa');
    expect(text()).toContain('Dự kiến trễ');
    expect(text()).toContain('chưa cấu hình ca làm việc');
    expect(fixture.nativeElement.querySelector('.operation-bar.provisional.late')).not.toBeNull();
  });

  it('changes horizon query and explicit refresh regenerates preview', async () => {
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];
    buttons.find(button => button.textContent?.includes('30 ngày'))?.click();
    await fixture.whenStable();
    expect(api.getPreview).toHaveBeenCalledWith(30);

    buttons.find(button => button.textContent?.includes('Tính lại kế hoạch'))?.click();
    await fixture.whenStable();
    expect(api.getPreview.calls.count()).toBe(3);
  });

  it('keeps the loaded 14-day horizon and Gantt geometry when a 30-day refresh fails', async () => {
    api.getPreview.and.returnValue(throwError(() => new Error('30-day request failed')));
    const horizonButton = (Array.from(
      fixture.nativeElement.querySelectorAll('button'),
    ) as HTMLButtonElement[]).find(button => button.textContent?.includes('30 ngày'))!;

    horizonButton.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.getPreview).toHaveBeenCalledWith(30);
    expect(horizonButton.getAttribute('aria-pressed')).toBe('true');
    expect(text()).toContain('Preview đang hiển thị: 14 ngày');
    expect(text()).toContain('Không thể kết nối tới FactoryMind API.');
    expect(fixture.nativeElement.querySelectorAll('.day-header span').length).toBe(14);
    expect((fixture.nativeElement.querySelector('.gantt') as HTMLElement).style.getPropertyValue('--day-count')).toBe('14');
    expect(text()).toContain('PO-001');
  });

  it('clips Gantt bars to both edges of the loaded horizon', () => {
    const component = fixture.componentInstance as unknown as {
      barStyle: (operation: ScheduleOperationPreview) => Record<string, string>;
    };
    const operation = preview().orders[0].operations[0];

    const clippedStart = component.barStyle({
      ...operation,
      scheduledStart: '2026-09-08T07:00:00Z',
      scheduledEnd: '2026-09-08T09:00:00Z',
    });
    const clippedEnd = component.barStyle({
      ...operation,
      scheduledStart: '2026-09-22T07:00:00Z',
      scheduledEnd: '2026-09-22T10:00:00Z',
    });

    expect(parseFloat(clippedStart['left'])).toBe(0);
    expect(parseFloat(clippedStart['width'])).toBe(0.35);
    expect(parseFloat(clippedEnd['left'])).toBeCloseTo(335 / 336 * 100, 5);
    expect(parseFloat(clippedEnd['width'])).toBeCloseTo(100 / 336, 5);
  });

  function text(): string { return fixture.nativeElement.textContent; }

  function preview(): SchedulePreview {
    return {
      generatedAt: '2026-09-08T08:00:00Z', horizonStart: '2026-09-08T08:00:00Z',
      horizonEnd: '2026-09-22T08:00:00Z', timeZoneId: 'Asia/Ho_Chi_Minh',
      summary: { ordersConsidered: 1, ordersScheduled: 1, projectedOnTime: 0, projectedLate: 1,
        unscheduled: 1, capacityConstrainedWorkCenters: 1, operationsScheduled: 1 },
      workCenters: [{ id: 'wc-1', code: 'WC-CNC', name: 'CNC', parallelCapacity: 2,
        availableCapacityMinutes: 9600, scheduledMinutes: 8300, unscheduledDemandMinutes: 60,
        plannedLoadPercent: 86.5, scheduledOperationCount: 1, unscheduledOperationCount: 1,
        hasCapacityConstraint: true, isActive: true, hasCalendar: true }],
      orders: [{ id: 'po-1', number: 'PO-001', productCode: 'P-01', productName: 'Part',
        status: 'planned', priority: 'urgent', deliveryStatus: 'due_soon', dueDate: '2026-09-09T08:00:00Z',
        planningSource: 'active_routing', isProvisional: true,
        projectedStart: '2026-09-08T08:00:00Z', projectedCompletion: '2026-09-09T09:00:00Z',
        projectedDeliveryStatus: 'projected_late', projectedLatenessMinutes: 60,
        operations: [{ id: 'op-1', sequence: 10, name: 'Cutting', workCenterId: 'wc-1',
          workCenterCode: 'WC-CNC', workCenterName: 'CNC', lane: 1, standardDurationMinutes: 60,
          plannedDurationMinutes: 60, scheduledStart: '2026-09-08T08:00:00Z',
          scheduledEnd: '2026-09-08T09:00:00Z', planningSource: 'active_routing',
          isProvisional: true, isInProgress: false }] }],
      unscheduled: [{ orderId: 'po-2', orderNumber: 'PO-002', operationId: 'op-2',
        operationName: 'Paint', workCenterId: 'wc-2', workCenterCode: 'WC-PAINT',
        demandMinutes: 60, reason: 'calendar_missing' }],
    };
  }
});
