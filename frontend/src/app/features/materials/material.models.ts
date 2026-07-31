export interface Material {
  id: string;
  code: string;
  name: string;
  unit: string;
  createdAt: string;
  updatedAt: string;
}

export interface MaterialInput {
  code: string;
  name: string;
  unit: string;
}
