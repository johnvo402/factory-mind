export type ProductionOrderStatus = 'planned' | 'in_progress' | 'completed' | 'cancelled';

export interface ProductionOrder {
  id: string;
  number: string;
  productId: string;
  productCode: string;
  productName: string;
  quantity: number;
  status: ProductionOrderStatus;
  createdAt: string;
  updatedAt: string;
}

export interface ProductionOrderInput {
  number: string;
  productId: string;
  quantity: number;
  status: ProductionOrderStatus;
}
