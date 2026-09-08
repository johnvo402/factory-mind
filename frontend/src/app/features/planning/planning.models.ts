export type ProjectedDeliveryStatus = 'unknown' | 'projected_on_time' | 'projected_late';

export interface SchedulePreviewSummary {
  ordersConsidered: number;
  ordersScheduled: number;
  projectedOnTime: number;
  projectedLate: number;
  unscheduled: number;
  capacityConstrainedWorkCenters: number;
  operationsScheduled: number;
}

export interface ScheduleOperationPreview {
  id: string;
  sequence: number;
  name: string;
  workCenterId: string;
  workCenterCode: string;
  workCenterName: string;
  lane: number;
  standardDurationMinutes: number;
  plannedDurationMinutes: number;
  scheduledStart: string;
  scheduledEnd: string;
  planningSource: string;
  isProvisional: boolean;
  isInProgress: boolean;
}

export interface ScheduleOrderPreview {
  id: string;
  number: string;
  productCode: string;
  productName: string;
  status: string;
  priority: string;
  deliveryStatus: string;
  dueDate: string | null;
  planningSource: string;
  isProvisional: boolean;
  projectedStart: string | null;
  projectedCompletion: string | null;
  projectedDeliveryStatus: ProjectedDeliveryStatus;
  projectedLatenessMinutes: number | null;
  operations: ScheduleOperationPreview[];
}

export interface WorkCenterCapacityPreview {
  id: string;
  code: string;
  name: string;
  parallelCapacity: number;
  availableCapacityMinutes: number;
  scheduledMinutes: number;
  unscheduledDemandMinutes: number;
  plannedLoadPercent: number | null;
  scheduledOperationCount: number;
  unscheduledOperationCount: number;
  hasCapacityConstraint: boolean;
  isActive: boolean;
  hasCalendar: boolean;
}

export interface UnscheduledOperationPreview {
  orderId: string;
  orderNumber: string;
  operationId: string | null;
  operationName: string | null;
  workCenterId: string | null;
  workCenterCode: string | null;
  demandMinutes: number;
  reason: string;
}

export interface SchedulePreview {
  generatedAt: string;
  horizonStart: string;
  horizonEnd: string;
  timeZoneId: string;
  summary: SchedulePreviewSummary;
  orders: ScheduleOrderPreview[];
  workCenters: WorkCenterCapacityPreview[];
  unscheduled: UnscheduledOperationPreview[];
}
