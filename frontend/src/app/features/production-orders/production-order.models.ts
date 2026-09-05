export type ProductionOrderStatus = 'planned' | 'released' | 'in_progress' | 'completed' | 'cancelled';
export type ProductionOperationStatus = 'pending' | 'in_progress' | 'completed';

export interface ProductionOrderOperation {
  id: string;
  productionOrderId: string;
  routingOperationId: string | null;
  sequence: number;
  name: string;
  workCenterId: string;
  workCenterCode: string;
  workCenterName: string;
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
}
