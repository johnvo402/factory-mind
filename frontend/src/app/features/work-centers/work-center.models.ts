export interface WorkCenter {
  id: string;
  code: string;
  name: string;
  description: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface WorkCenterInput {
  code: string;
  name: string;
  description: string | null;
}
