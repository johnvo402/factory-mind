export interface Product {
  id: string;
  code: string;
  name: string;
  createdAt: string;
  updatedAt: string;
}

export interface ProductInput {
  code: string;
  name: string;
}
