export type BomStatus = 'draft' | 'active' | 'archived';

export interface BomItem {
  id: string;
  materialId: string;
  materialCode: string;
  materialName: string;
  unit: string;
  quantity: number;
  scrapPercentage: number | null;
  createdAt: string;
  updatedAt: string;
}

export interface Bom {
  id: string;
  productId: string;
  productCode: string;
  productName: string;
  revision: number;
  outputQuantity: number;
  status: BomStatus;
  items: BomItem[];
  createdAt: string;
  updatedAt: string;
}

export interface BomItemInput {
  materialId: string;
  quantity: number;
  scrapPercentage: number | null;
}

export interface BomInput {
  outputQuantity: number;
  items: BomItemInput[];
}

export interface MaterialRequirement {
  materialId: string;
  materialCode: string;
  materialName: string;
  unit: string;
  quantityPerBom: number;
  scrapPercentage: number | null;
  requiredQuantity: number;
  availableQuantity: number;
  shortageQuantity: number;
  isSufficient: boolean;
}

export interface MaterialRequirements {
  productId: string;
  productCode: string;
  productName: string;
  bomId: string;
  bomRevision: number;
  requestedQuantity: number;
  canProduce: boolean;
  materials: MaterialRequirement[];
}
