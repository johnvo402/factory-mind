export interface ProductInventoryBalance {
  id: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  productId: string;
  productCode: string;
  productName: string;
  quantity: number;
  updatedAt: string;
}

export type ProductInventoryTransactionType = 'ProductionOutput';

export interface ProductInventoryTransaction {
  id: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  productId: string;
  productCode: string;
  productName: string;
  type: ProductInventoryTransactionType;
  quantity: number;
  signedQuantity: number;
  referenceType: string | null;
  referenceId: string | null;
  note: string | null;
  createdAt: string;
}

export interface ProductInventoryTransactionPage {
  items: ProductInventoryTransaction[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ProductInventoryBalanceFilters {
  warehouseId?: string;
  productId?: string;
  search?: string;
}

export interface ProductInventoryTransactionFilters {
  warehouseId?: string;
  productId?: string;
  transactionType?: ProductInventoryTransactionType;
  from?: string;
  to?: string;
  page: number;
  pageSize: number;
}
