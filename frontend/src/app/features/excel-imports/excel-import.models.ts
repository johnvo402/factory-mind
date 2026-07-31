export type ExcelImportEntityType =
  | 'machine'
  | 'material'
  | 'product'
  | 'inventory'
  | 'production_order';

export interface ExcelPreview {
  headers: string[];
  rows: Record<string, string>[];
  totalRows: number;
  requiredFields: string[];
  suggestedMapping: Record<string, string>;
}

export interface ExcelRowError {
  row: number;
  field: string;
  message: string;
}

export interface ExcelImportResult {
  importedCount: number;
  errors: ExcelRowError[];
}
