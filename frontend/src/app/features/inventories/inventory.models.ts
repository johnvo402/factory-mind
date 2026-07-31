export interface Inventory {
  id: string;
  materialId: string;
  materialCode: string;
  materialName: string;
  unit: string;
  warehouse: string;
  quantity: number;
  createdAt: string;
  updatedAt: string;
}

export interface InventoryInput {
  materialId: string;
  warehouse: string;
  quantity: number;
}
