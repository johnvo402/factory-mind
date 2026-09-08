export type ProductionOrderStatus =
  'planned' | 'released' | 'in_progress' | 'completed' | 'cancelled';
export type ProductionOperationStatus = 'pending' | 'in_progress' | 'completed';
export type ProductionOrderPriority = 'low' | 'normal' | 'high' | 'urgent';
export type ProductionOrderDeliveryStatus =
  | 'no_due_date'
  | 'on_track'
  | 'due_soon'
  | 'overdue'
  | 'completed_on_time'
  | 'completed_late'
  | 'cancelled';
export type ProductionOrderSortField =
  | 'deliveryRisk'
  | 'updatedAt'
  | 'dueDate'
  | 'priority'
  | 'number'
  | 'status';
export type SortDirection = 'asc' | 'desc';

export interface ProductionOrderOperation {
  id: string;
  productionOrderId: string;
  routingOperationId: string | null;
  sequence: number;
  name: string;
  workCenterId: string;
  workCenterCode: string;
  workCenterName: string;
  machineId: string | null;
  machineCode: string | null;
  machineName: string | null;
  setupTimeMinutes: number;
  runTimeMinutes: number;
  description: string | null;
  status: ProductionOperationStatus;
  startedAt: string | null;
  completedAt: string | null;
  createdAt: string;
}

export interface ProductionOrder {
  id: string;
  number: string;
  productId: string;
  productCode: string;
  productName: string;
  quantity: number;
  status: ProductionOrderStatus;
  dueDate: string | null;
  priority: ProductionOrderPriority;
  deliveryStatus: ProductionOrderDeliveryStatus;
  daysUntilDue: number | null;
  isOverdue: boolean;
  isDueSoon: boolean;
  isCompletedLate: boolean;
  billOfMaterialId: string | null;
  bomRevision: number | null;
  routingId: string | null;
  routingRevision: number | null;
  operations: ProductionOrderOperation[];
  releasedAt: string | null;
  startedAt: string | null;
  completedAt: string | null;
  cancelledAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface ProductionOrderInput {
  number: string;
  productId: string;
  quantity: number;
  dueDate: string | null;
  priority: ProductionOrderPriority;
}

export interface ProductionOrderPage {
  items: ProductionOrder[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ProductionOrderFilters {
  search?: string;
  status?: ProductionOrderStatus;
  priority?: ProductionOrderPriority;
  deliveryStatus?: ProductionOrderDeliveryStatus;
  dueFrom?: string;
  dueTo?: string;
  productId?: string;
  page: number;
  pageSize: number;
  sortBy: ProductionOrderSortField;
  sortDirection: SortDirection;
}

export interface ProductionMaterialAllocationInput {
  materialId: string;
  warehouseId: string;
  quantity: number;
}

export interface StartProductionOrderInput {
  allocations: ProductionMaterialAllocationInput[];
}

export interface CompleteProductionOrderInput {
  warehouseId: string;
}
