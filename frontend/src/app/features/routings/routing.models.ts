export type RoutingStatus = 'draft' | 'active' | 'archived';

export interface RoutingOperation {
  id: string;
  sequence: number;
  name: string;
  workCenterId: string;
  workCenterCode: string;
  workCenterName: string;
  setupTimeMinutes: number;
  runTimeMinutes: number;
  description: string | null;
}

export interface Routing {
  id: string;
  productId: string;
  revision: number;
  status: RoutingStatus;
  operations: RoutingOperation[];
  createdAt: string;
  updatedAt: string;
}

export interface RoutingInput {
  operations: Array<{
    sequence: number;
    name: string;
    workCenterId: string;
    setupTimeMinutes: number;
    runTimeMinutes: number;
    description: string | null;
  }>;
}
