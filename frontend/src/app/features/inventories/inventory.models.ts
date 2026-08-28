export interface Inventory {
  id: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  materialId: string;
  materialCode: string;
  materialName: string;
  unit: string;
  quantity: number;
  updatedAt: string;
}

export interface Warehouse {
  id: string;
  code: string;
  name: string;
  description: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export type InventoryTransactionType =
  | 'Receipt'
  | 'Issue'
  | 'AdjustmentIncrease'
  | 'AdjustmentDecrease'
  | 'TransferIn'
  | 'TransferOut'
  | 'ProductionConsume'
  | 'ProductionOutput';

export interface InventoryTransaction {
  id: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  materialId: string;
  materialCode: string;
  materialName: string;
  unit: string;
  type: InventoryTransactionType;
  quantity: number;
  signedQuantity: number;
  referenceType: string | null;
  referenceId: string | null;
  note: string | null;
  createdAt: string;
}

export interface InventoryTransactionPage {
  items: InventoryTransaction[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface InventoryMovementInput {
  warehouseId: string;
  materialId: string;
  quantity: number;
  note: string | null;
  referenceType: string | null;
  referenceId: string | null;
}

export interface InventoryAdjustmentInput extends InventoryMovementInput {
  direction: 'Increase' | 'Decrease';
}

export interface InventoryTransferInput {
  sourceWarehouseId: string;
  destinationWarehouseId: string;
  materialId: string;
  quantity: number;
  note: string | null;
  referenceType: string | null;
}

export interface WarehouseCreateInput {
  code: string;
  name: string;
  description: string | null;
}

export interface WarehouseUpdateInput extends WarehouseCreateInput {
  isActive: boolean;
}
